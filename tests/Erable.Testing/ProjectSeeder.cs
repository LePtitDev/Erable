using System.Text.Json;
using Dapper;
using Erable.Core;
using Erable.Core.Attachments;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Projects;
using Erable.Core.Properties;
using Erable.Core.Tree;
using Erable.Storage;
using Microsoft.Data.Sqlite;

namespace Erable.Testing;

/// <summary>
/// Writes the elements of a <see cref="ProjectBuilder"/> into a real project folder, with their declared IDs, stamps,
/// contents, attachment files, categories, property definitions and values. Derived data (plain text, references,
/// resolved values, validation issues) is computed by the same code as the application, so a seeded project is
/// indistinguishable from one written through the API.
/// </summary>
public static class ProjectSeeder
{
    public static readonly DateTimeOffset DefaultTime = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    /// <summary>Creates the project in <paramref name="folder"/> and returns it open (the caller disposes it).</summary>
    public static OpenedProject Create(
        ProjectBuilder builder, string folder, string name = "Atlas", string contentLanguage = "fr",
        ElementId? projectId = null, DateTimeOffset? createdAt = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var created = createdAt ?? DefaultTime;
        var project = ProjectStore.Create(folder, name, contentLanguage, new FixedClock(created)).Value;
        try
        {
            Seed(project, builder, created);
            if (projectId is { } fixedId)
            {
                ManifestFile.Write(project.Folder, project.Manifest with { ProjectId = fixedId });
                project.Dispose();
                return ProjectStore.Open(folder, new FixedClock(created)).Value;
            }

            return project;
        }
        catch
        {
            project.Dispose();
            throw;
        }
    }

    private static void Seed(OpenedProject project, ProjectBuilder builder, DateTimeOffset defaultTime)
    {
        var tree = ElementTree.CreateEmpty();
        var blobs = new List<StoredBlob>();
        try
        {
            // Blobs first (outside the transaction), exactly like an upload.
            var stored = new Dictionary<ElementId, StoredBlob>();
            foreach (var entry in builder.Entries.Where(e => e.Content is not null))
            {
                var blob = project.Blobs.WriteAsync(new MemoryStream(entry.Content!), long.MaxValue).GetAwaiter().GetResult()!;
                blobs.Add(blob);
                stored[entry.Id] = blob;
            }

            project.Database.Write((connection, transaction) =>
            {
                var markdowns = new Dictionary<ElementId, string>();
                foreach (var entry in builder.Entries)
                {
                    var created = tree.Create(entry.Id, entry.Kind, entry.ParentId, entry.Name);
                    if (!created.IsSuccess)
                    {
                        throw new InvalidOperationException($"Seeder: cannot create '{entry.Name}': {created.Error.Code}.");
                    }

                    var stamp = entry.Stamp ?? new ProjectBuilder.Stamp(defaultTime, KnownUsers.Local, defaultTime, KnownUsers.Local);
                    var node = created.Value.Node;
                    var element = new Element(
                        entry.Id, entry.Kind, entry.ParentId, node.Name, node.SortOrder, 1,
                        stamp.CreatedAt, stamp.CreatedBy, stamp.UpdatedAt, stamp.UpdatedBy, null);
                    ProjectRepository.InsertElement(connection, transaction, element);

                    if (entry.Kind == ElementKind.Page)
                    {
                        var markdown = builder.ResolveMarkdown(entry.Markdown ?? string.Empty);
                        var analysis = ErableMarkdown.Analyze(markdown);
                        ProjectRepository.UpsertPageContent(connection, transaction, entry.Id, markdown, analysis.PlainText);
                        ProjectRepository.ReplaceReferences(connection, transaction, entry.Id,
                            analysis.References.Select(r => new StoredReference(entry.Id, r.Id, r.Anchor, r.IsEmbed, r.Start)));
                        markdowns[entry.Id] = markdown;
                    }
                    else if (stored.TryGetValue(entry.Id, out var blob))
                    {
                        var mime = MediaTypes.Detect(blob.Head.Span, entry.Name);
                        ProjectRepository.InsertAttachment(connection, transaction,
                            new AttachmentRecord(entry.Id, entry.ParentId, entry.Name, mime, blob.Size, blob.Sha256));
                    }
                }

                SeedStructure(connection, transaction, builder);
                StructureState.Load(connection, null).WriteDerived(connection, transaction, null, null);

                // Revisions last: their snapshots hold the categories, values and definitions written above.
                foreach (var entry in builder.Entries)
                {
                    var element = project.Repository.GetElement(entry.Id, connection)!;
                    var snapshot = ProjectService.Snapshot(connection, element, markdowns.GetValueOrDefault(entry.Id));
                    string? sha = null;
                    if (stored.ContainsKey(entry.Id))
                    {
                        var attachment = project.Repository.GetAttachment(entry.Id, connection)!;
                        sha = attachment.Sha256;
                        snapshot = snapshot with { Attachment = new AttachmentSnapshot(attachment.FileName, attachment.Mime, attachment.Size, sha) };
                    }

                    ProjectRepository.InsertRevision(connection, transaction, entry.Id, 1, element.UpdatedBy, element.UpdatedAt, snapshot.Serialize(), sha);
                }

                return 0;
            });
        }
        finally
        {
            foreach (var blob in blobs)
            {
                blob.Dispose();
            }
        }
    }

    /// <summary>Category, definition and link type rows, then values and page categories (schema v2).</summary>
    private static void SeedStructure(SqliteConnection connection, SqliteTransaction transaction, ProjectBuilder builder)
    {
        var keys = new List<string>();
        var definitions = new Dictionary<ElementId, PropertyDefinition>();
        foreach (var entry in builder.Entries)
        {
            switch (entry.Kind)
            {
                case ElementKind.Category:
                    var parent = entry.Category?.Parent is { } p ? builder.IdOf(p) : ElementId.None;
                    StructureRepository.UpsertCategory(connection, transaction, new CategoryRecord(entry.Id, parent, entry.Category?.Description ?? string.Empty));
                    break;
                case ElementKind.PropertyDefinition:
                    var definition = DefinitionOf(builder, entry, keys);
                    keys.Add(definition.Key);
                    definitions[entry.Id] = definition;
                    StructureRepository.UpsertDefinition(connection, transaction, definition);
                    break;
                case ElementKind.LinkType:
                    connection.Execute("INSERT INTO link_types (id, description, inverse_name) VALUES (@Id, '', NULL)", new { Id = entry.Id.ToInt64() }, transaction);
                    break;
                default:
                    break;
            }
        }

        foreach (var entry in builder.Entries)
        {
            if ((entry.Category?.Values ?? entry.Page?.Values) is { } values)
            {
                StructureRepository.ReplaceValues(connection, transaction, entry.Id, values.Select(v =>
                {
                    var definition = definitions[builder.IdOf(v.Key)];
                    return (definition.Id, PropertyValues.FromJson(builder.ResolveJson(v.Value), definition.Type, definition.Options).Value);
                }));
            }

            if (entry.Page is { } page)
            {
                StructureRepository.ReplacePageCategories(connection, transaction, entry.Id, [.. page.Categories.Select(builder.IdOf)]);
            }
        }
    }

    private static PropertyDefinition DefinitionOf(ProjectBuilder builder, ProjectBuilder.Entry entry, IReadOnlyList<string> keys)
    {
        var spec = entry.Property ?? new ProjectBuilder.PropertySpec("string", "[]", [], null, null, string.Empty);
        var type = PropertyType.Parse(spec.Type).Value;
        var options = spec.Options.Select(o => new EnumOption(o.Key, o.Label)).ToArray();
        using var rules = JsonDocument.Parse(builder.ResolveJson(spec.Rules));
        var key = spec.Key ?? PropertyKey.Unique(PropertyKey.FromName(entry.Name), keys);
        var @default = spec.Default is null ? null : PropertyValues.FromJson(builder.ResolveJson(spec.Default), type, options).Value;
        var definition = new PropertyDefinition(
            entry.Id, entry.Name, key, spec.Description, type, @default, ValidationRules.ListFromJson(rules.RootElement, type, options).Value, options);
        return definition.Check() is { } error ? throw new InvalidOperationException($"Seeder: property '{entry.Name}': {error.Code}.") : definition;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
