using System.Text.RegularExpressions;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Tree;

namespace Erable.Testing;

/// <summary>
/// Entry point of the fluent test builders (docs/testing.md §4.3). Elements are named once and referenced by name,
/// so tests read like the reference project:
/// <code>
/// var project = Project.Build()
///     .WithFolder("Personnages")
///     .WithFolder("PNJ", under: "Personnages")
///     .WithPage("Ilsa Varenne", under: "PNJ", id: "3F2A9C14E07B5D21", markdown: "Voir [[@Capitaine Orsen]].");
/// var tree = project.BuildTree();                          // in memory (Core)
/// using var opened = ProjectSeeder.Create(project, dir);   // a real project folder (Storage)
/// </code>
/// Markdown may name elements instead of IDs: <c>[[@Name]]</c>, <c>[[@Name|label]]</c>, <c>![[@Name]]</c> are replaced
/// by their IDs when the project is written, so elements can reference ones declared later.
/// </summary>
public static class Project
{
    public static ProjectBuilder Build() => new();
}

public sealed partial class ProjectBuilder
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<string, ElementId> _ids = new(StringComparer.Ordinal);
    private Func<string, ElementId> _idFactory = _ => ElementId.NewRandom();

    /// <summary>Name → ID of every element declared so far (roots: "Pages", "Catégories", "Propriétés", "Liens").</summary>
    public IReadOnlyDictionary<string, ElementId> Ids => _ids;

    public ProjectBuilder()
    {
        _ids["Pages"] = Roots.Pages.Id;
        _ids["Catégories"] = Roots.Categories.Id;
        _ids["Propriétés"] = Roots.Properties.Id;
        _ids["Liens"] = Roots.Links.Id;
    }

    public IReadOnlyList<Entry> Entries => _entries;

    public ElementId IdOf(string name) =>
        _ids.TryGetValue(name, out var id) ? id : throw new KeyNotFoundException($"No element named '{name}' in the builder.");

    /// <summary>How IDs are chosen when a declaration gives none (random by default; deterministic for the reference project).</summary>
    public ProjectBuilder WithIdFactory(Func<string, ElementId> factory)
    {
        _idFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>A folder, under <paramref name="under"/> (a name) or the root of <paramref name="root"/>'s kind.</summary>
    public ProjectBuilder WithFolder(string name, string? under = null, ElementKind root = ElementKind.Page, string? id = null, string? key = null) =>
        Add(name, ElementKind.Folder, under ?? RootName(root), id, null, null, key);

    public ProjectBuilder WithPage(string name, string? under = null, string? id = null, string? markdown = null) =>
        Add(name, ElementKind.Page, under ?? "Pages", id, markdown, null, null);

    /// <summary>A file attached to the page <paramref name="page"/>; its name is the file name.</summary>
    public ProjectBuilder WithAttachment(string fileName, string page, byte[] content, string? id = null) =>
        Add(fileName, ElementKind.Attachment, page, id, null, content ?? throw new ArgumentNullException(nameof(content)), null);

    public ProjectBuilder WithCategory(string name, string? under = null, string? id = null, string? key = null) =>
        Add(name, ElementKind.Category, under ?? "Catégories", id, null, null, key);

    public ProjectBuilder WithProperty(string name, string? under = null, string? id = null, string? key = null) =>
        Add(name, ElementKind.PropertyDefinition, under ?? "Propriétés", id, null, null, key);

    public ProjectBuilder WithLinkType(string name, string? under = null, string? id = null, string? key = null) =>
        Add(name, ElementKind.LinkType, under ?? "Liens", id, null, null, key);

    /// <summary>Creation and last modification of an element already declared (defaults: the seeding time, <c>local</c>).</summary>
    public ProjectBuilder Stamped(string name, DateTimeOffset createdAt, string createdBy, DateTimeOffset? updatedAt = null, string? updatedBy = null)
    {
        var index = _entries.FindIndex(e => e.Key == name);
        if (index < 0)
        {
            throw new KeyNotFoundException($"No element named '{name}' in the builder.");
        }

        _entries[index] = _entries[index] with { Stamp = new Stamp(createdAt, createdBy, updatedAt ?? createdAt, updatedBy ?? createdBy) };
        return this;
    }

    /// <summary>The explorer tree of the declared elements; fails if a declaration breaks a tree rule.</summary>
    public ElementTree BuildTree()
    {
        var tree = ElementTree.CreateEmpty();
        foreach (var entry in _entries)
        {
            var result = tree.Create(entry.Id, entry.Kind, entry.ParentId, entry.Name);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException($"Builder: cannot create '{entry.Name}': {result.Error.Code}.");
            }
        }

        return tree;
    }

    /// <summary>Replaces the <c>@Name</c> placeholders of a Markdown text by the IDs of the named elements.</summary>
    public string ResolveMarkdown(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Placeholder().Replace(markdown, m => m.Groups["open"].Value + IdOf(m.Groups["name"].Value));
    }

    /// <param name="key">Builder key of the element, when its name is not unique (two folders « Personnages »).</param>
    private ProjectBuilder Add(string name, ElementKind kind, string under, string? id, string? markdown, byte[]? content, string? key)
    {
        key ??= name;
        if (_ids.ContainsKey(key))
        {
            throw new ArgumentException($"Builder: '{key}' is already declared; keys (names by default) are unique.", nameof(name));
        }

        var elementId = id is null ? _idFactory(key) : ElementId.Parse(id);
        _ids[key] = elementId;
        _entries.Add(new Entry(elementId, kind, IdOf(under), name, markdown, content, null) { Key = key });
        return this;
    }

    private static string RootName(ElementKind kind) => kind switch
    {
        ElementKind.Category => "Catégories",
        ElementKind.PropertyDefinition => "Propriétés",
        ElementKind.LinkType => "Liens",
        _ => "Pages",
    };

    [GeneratedRegex(@"(?<open>!?\[\[)@(?<name>[^\]|#]+)")]
    private static partial Regex Placeholder();

    /// <summary>Who created and last modified an element, and when.</summary>
    public sealed record Stamp(DateTimeOffset CreatedAt, string CreatedBy, DateTimeOffset UpdatedAt, string UpdatedBy);

    /// <summary>One declared element, in declaration order.</summary>
    public sealed record Entry(
        ElementId Id, ElementKind Kind, ElementId ParentId, string Name, string? Markdown, byte[]? Content, Stamp? Stamp)
    {
        /// <summary>Builder key (the name unless declared otherwise).</summary>
        public string Key { get; init; } = Name;
    }
}
