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
///     .WithPage("Ilsa Varenne", under: "PNJ", id: "3F2A9C14E07B5D21");
/// var tree = project.BuildTree();
/// </code>
/// </summary>
public static class Project
{
    public static ProjectBuilder Build() => new();
}

public sealed class ProjectBuilder
{
    private readonly List<Entry> _entries = [];
    private readonly Dictionary<string, ElementId> _ids = new(StringComparer.Ordinal);

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

    /// <summary>A folder, under <paramref name="under"/> (a name) or the root of <paramref name="root"/>'s kind.</summary>
    public ProjectBuilder WithFolder(string name, string? under = null, ElementKind root = ElementKind.Page, string? id = null) =>
        Add(name, ElementKind.Folder, under ?? RootName(root), id, null);

    public ProjectBuilder WithPage(string name, string? under = null, string? id = null, string? markdown = null) =>
        Add(name, ElementKind.Page, under ?? "Pages", id, markdown);

    public ProjectBuilder WithCategory(string name, string? under = null, string? id = null) =>
        Add(name, ElementKind.Category, under ?? "Catégories", id, null);

    public ProjectBuilder WithProperty(string name, string? under = null, string? id = null) =>
        Add(name, ElementKind.PropertyDefinition, under ?? "Propriétés", id, null);

    public ProjectBuilder WithLinkType(string name, string? under = null, string? id = null) =>
        Add(name, ElementKind.LinkType, under ?? "Liens", id, null);

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

    private ProjectBuilder Add(string name, ElementKind kind, string under, string? id, string? markdown)
    {
        if (_ids.ContainsKey(name))
        {
            throw new ArgumentException($"Builder: '{name}' is already declared; names are the builder's keys.", nameof(name));
        }

        var elementId = id is null ? ElementId.NewRandom() : ElementId.Parse(id);
        _ids[name] = elementId;
        _entries.Add(new Entry(elementId, kind, IdOf(under), name, markdown));
        return this;
    }

    private static string RootName(ElementKind kind) => kind switch
    {
        ElementKind.Category => "Catégories",
        ElementKind.PropertyDefinition => "Propriétés",
        ElementKind.LinkType => "Liens",
        _ => "Pages",
    };

    /// <summary>One declared element, in declaration order.</summary>
    public sealed record Entry(ElementId Id, ElementKind Kind, ElementId ParentId, string Name, string? Markdown);
}
