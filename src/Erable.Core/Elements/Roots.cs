namespace Erable.Core.Elements;

/// <summary>One of the four fixed roots of the explorer.</summary>
/// <param name="Id">Stable ID, identical in every project (part of the format).</param>
/// <param name="Key">Stable key; the label is looked up from it, never stored (docs/architecture.md §11.1, point 3).</param>
/// <param name="ItemKind">Kind of the elements filed under this root (besides folders).</param>
public sealed record RootDefinition(ElementId Id, string Key, ElementKind ItemKind);

/// <summary>The four roots (F02.R1). Their IDs are reserved: <see cref="ElementId.NewRandom"/> never produces them.</summary>
public static class Roots
{
    public static readonly RootDefinition Pages = new(new ElementId(1), "pages", ElementKind.Page);
    public static readonly RootDefinition Categories = new(new ElementId(2), "categories", ElementKind.Category);
    public static readonly RootDefinition Properties = new(new ElementId(3), "properties", ElementKind.PropertyDefinition);
    public static readonly RootDefinition Links = new(new ElementId(4), "links", ElementKind.LinkType);

    /// <summary>In display order.</summary>
    public static readonly IReadOnlyList<RootDefinition> All = [Pages, Categories, Properties, Links];

    public static RootDefinition? Find(ElementId id) => All.FirstOrDefault(r => r.Id == id);

    public static RootDefinition ForItemKind(ElementKind kind) =>
        All.FirstOrDefault(r => r.ItemKind == kind)
        ?? throw new ArgumentOutOfRangeException(nameof(kind), kind, "This kind has no root of its own.");

    /// <summary>
    /// Label of a root in the project's <b>content</b> language (not the interface language, decision Q1).
    /// Unknown languages fall back to English.
    /// </summary>
    public static string Label(RootDefinition root, string contentLanguage)
    {
        ArgumentNullException.ThrowIfNull(root);
        var labels = Labels.TryGetValue(ContentLanguages.Normalize(contentLanguage), out var found) ? found : Labels["en"];
        return labels[root.Key];
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Labels = new(StringComparer.Ordinal)
    {
        ["fr"] = new(StringComparer.Ordinal) { ["pages"] = "Pages", ["categories"] = "Catégories", ["properties"] = "Propriétés", ["links"] = "Liens" },
        ["en"] = new(StringComparer.Ordinal) { ["pages"] = "Pages", ["categories"] = "Categories", ["properties"] = "Properties", ["links"] = "Links" },
        ["de"] = new(StringComparer.Ordinal) { ["pages"] = "Seiten", ["categories"] = "Kategorien", ["properties"] = "Eigenschaften", ["links"] = "Verknüpfungen" },
        ["es"] = new(StringComparer.Ordinal) { ["pages"] = "Páginas", ["categories"] = "Categorías", ["properties"] = "Propiedades", ["links"] = "Enlaces" },
    };
}

/// <summary>Content languages a project can be created with (F01.R4); same list as the interface languages.</summary>
public static class ContentLanguages
{
    public static readonly IReadOnlyList<string> All = ["fr", "en", "de", "es"];

    public static bool IsSupported(string? language) => language is not null && All.Contains(Normalize(language));

    /// <summary>Base language of a BCP 47 tag, lower case: <c>fr-CA</c> → <c>fr</c>.</summary>
    public static string Normalize(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var dash = language.IndexOf('-', StringComparison.Ordinal);
        return (dash < 0 ? language : language[..dash]).ToLowerInvariant();
    }
}
