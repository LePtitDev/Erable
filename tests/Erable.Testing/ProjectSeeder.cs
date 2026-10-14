using Erable.Core;
using Erable.Core.Attachments;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Projects;
using Erable.Core.Tree;
using Erable.Storage;

namespace Erable.Testing;

/// <summary>
/// Writes the elements of a <see cref="ProjectBuilder"/> into a real project folder, with their declared IDs, stamps,
/// contents and attachment files. Derived data (plain text, references) is computed by the same Core code as the
/// application, so a seeded project is indistinguishable from one written through the API.
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

                    string? markdown = null;
                    AttachmentSnapshot? attachment = null;
                    if (entry.Kind == ElementKind.Page)
                    {
                        markdown = builder.ResolveMarkdown(entry.Markdown ?? string.Empty);
                        var analysis = ErableMarkdown.Analyze(markdown);
                        ProjectRepository.UpsertPageContent(connection, transaction, entry.Id, markdown, analysis.PlainText);
                        ProjectRepository.ReplaceReferences(connection, transaction, entry.Id,
                            analysis.References.Select(r => new StoredReference(entry.Id, r.Id, r.Anchor, r.IsEmbed, r.Start)));
                    }
                    else if (stored.TryGetValue(entry.Id, out var blob))
                    {
                        var mime = MediaTypes.Detect(blob.Head.Span, entry.Name);
                        ProjectRepository.InsertAttachment(connection, transaction,
                            new AttachmentRecord(entry.Id, entry.ParentId, entry.Name, mime, blob.Size, blob.Sha256));
                        attachment = new AttachmentSnapshot(entry.Name, mime, blob.Size, blob.Sha256);
                    }

                    var snapshot = new RevisionSnapshot(entry.Id, entry.Kind, entry.ParentId, node.Name, markdown, attachment);
                    ProjectRepository.InsertRevision(connection, transaction, entry.Id, 1, stamp.UpdatedBy, stamp.UpdatedAt,
                        snapshot.Serialize(), attachment?.Sha256);
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

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
