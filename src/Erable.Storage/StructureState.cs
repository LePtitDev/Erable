using System.Diagnostics.CodeAnalysis;
using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>Origin of the effective value of a property on a page.</summary>
public enum ValueOrigin
{
    /// <summary>Defined on the page.</summary>
    Own,

    /// <summary>Provided by a category (« via PNJ »).</summary>
    Inherited,

    /// <summary>Default of the property definition.</summary>
    Default,

    /// <summary>Unrelated categories disagree: no effective value until the page fixes one.</summary>
    Conflict,
}

/// <summary>A property of a page after inheritance, with its rule violations (F04.R4, F07.R3, F08.R3).</summary>
public sealed record ResolvedProperty(
    ElementId PageId,
    PropertyDefinition Definition,
    PropertyValue? OwnValue,
    Resolution<PropertyValue> Resolution,
    IReadOnlyList<RuleViolation> Violations)
{
    public ValueOrigin Origin => Resolution switch
    {
        Resolution<PropertyValue>.Own => ValueOrigin.Own,
        Resolution<PropertyValue>.Inherited => ValueOrigin.Inherited,
        Resolution<PropertyValue>.FromDefinition => ValueOrigin.Default,
        _ => ValueOrigin.Conflict,
    };
}

/// <summary>
/// What inheritance and validation need, loaded in one go from a connection: the live tree, the category graph, the
/// definitions, every category value, and the values and categories of the pages in scope. The single place where
/// values are resolved, so that the page view, the derived tables and <c>erable doctor</c> always agree.
/// </summary>
public sealed class StructureState : IRuleContext
{
    private readonly Dictionary<(ElementId Owner, ElementId Property), PropertyValue> _values = [];
    private readonly Dictionary<ElementId, IReadOnlyList<ElementId>> _pageCategories;

    private StructureState(
        ElementTree tree, CategoryGraph graph, IReadOnlyDictionary<ElementId, CategoryRecord> categories,
        IReadOnlyDictionary<ElementId, PropertyDefinition> definitions, Dictionary<ElementId, IReadOnlyList<ElementId>> pageCategories)
    {
        Tree = tree;
        Graph = graph;
        Categories = categories;
        Definitions = definitions;
        _pageCategories = pageCategories;
    }

    public ElementTree Tree { get; }

    /// <summary>Inheritance graph of the live categories.</summary>
    public CategoryGraph Graph { get; }

    /// <summary>Live categories.</summary>
    public IReadOnlyDictionary<ElementId, CategoryRecord> Categories { get; }

    /// <summary>Live property definitions.</summary>
    public IReadOnlyDictionary<ElementId, PropertyDefinition> Definitions { get; }

    /// <summary>
    /// Loads the state for <paramref name="pages"/> (every page when null): their values, plus every category value and
    /// every page-category link (few rows, needed by any resolution and by the <c>in_category</c> rule).
    /// </summary>
    public static StructureState Load(SqliteConnection connection, IReadOnlyCollection<ElementId>? pages, ElementTree? tree = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        tree ??= ElementTree.Load(ProjectRepository.ReadTreeNodes(connection));
        var live = tree;
        var categories = StructureRepository.LoadCategories(connection).Where(c => live.Contains(c.Id)).ToDictionary(c => c.Id);
        var graph = CategoryGraph.Load(categories.Values.Select(c => new CategoryNode(c.Id, c.ParentCategoryId)));
        var definitions = StructureRepository.LoadDefinitions(connection).Where(d => live.Contains(d.Id)).ToDictionary(d => d.Id);
        var state = new StructureState(tree, graph, categories, definitions, StructureRepository.LoadPageCategories(connection, null));
        IReadOnlyCollection<ElementId>? owners = pages is null ? null : [.. categories.Keys, .. pages];
        foreach (var value in StructureRepository.LoadValues(connection, definitions, owners))
        {
            state._values[(value.OwnerId, value.PropertyId)] = value.Value;
        }

        return state;
    }

    /// <summary>Live pages: every one in the tree, or those of the scope.</summary>
    public IEnumerable<ElementId> LivePages(IReadOnlyCollection<ElementId>? pages) =>
        (pages ?? Tree.Nodes.Where(n => n.Kind == ElementKind.Page).Select(n => n.Id))
            .Where(p => Tree.Find(p) is { Kind: ElementKind.Page });

    /// <summary>Live categories of a page, in their order.</summary>
    public IReadOnlyList<ElementId> CategoriesOf(ElementId pageId) =>
        _pageCategories.TryGetValue(pageId, out var categories) ? [.. categories.Where(Categories.ContainsKey)] : [];

    public PropertyValue? ValueOf(ElementId owner, ElementId property) => _values.GetValueOrDefault((owner, property));

    /// <summary>Own values of an owner (page or category), by property, for live definitions.</summary>
    public IReadOnlyList<(ElementId Property, PropertyValue Value)> ValuesOf(ElementId owner) =>
        [.. _values.Where(v => v.Key.Owner == owner).Select(v => (v.Key.Property, v.Value)).OrderBy(v => v.Property.Value)];

    /// <summary>
    /// The properties of a page after inheritance: those it defines, those its categories provide (conflicts included),
    /// and those whose definition has a default. Ordered by property name.
    /// </summary>
    public IReadOnlyList<ResolvedProperty> ResolvePage(ElementId pageId, IReadOnlyCollection<ElementId>? properties = null)
    {
        var categories = CategoriesOf(pageId);
        var chains = categories.SelectMany(c => (IEnumerable<ElementId>)[c, .. Graph.Ancestors(c)]).ToHashSet();
        var relevant = Definitions.Values.Where(d =>
            (properties is null || properties.Contains(d.Id))
            && (_values.ContainsKey((pageId, d.Id)) || d.Default is not null || chains.Any(c => _values.ContainsKey((c, d.Id)))));
        var result = new List<ResolvedProperty>();
        foreach (var definition in relevant.OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(d => d.Id.Value))
        {
            var resolution = ValueResolver.ForPage(Graph, pageId, categories, Lookup(definition.Id), definition.Default);
            var violations = resolution.EffectiveValue is { } value ? definition.Validate(value, this) : [];
            result.Add(new ResolvedProperty(pageId, definition, ValueOf(pageId, definition.Id), resolution, violations));
        }

        return result;
    }

    /// <summary>Value of a property for a category: own, inherited from an ancestor, or the definition default.</summary>
    public Resolution<PropertyValue> ResolveCategory(ElementId categoryId, PropertyDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return ValueResolver.ForCategory(Graph, categoryId, Lookup(definition.Id), definition.Default);
    }

    /// <summary>Properties defined on a category or one of its ancestors (the property table of the category screen).</summary>
    public IReadOnlyList<PropertyDefinition> PropertiesOfCategory(ElementId categoryId)
    {
        var chain = Graph.Lineage(categoryId);
        return [.. Definitions.Values.Where(d => chain.Any(c => _values.ContainsKey((c, d.Id))))
            .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(d => d.Id.Value)];
    }

    /// <summary>Rows of the derived tables for the pages of the scope (all live pages when null).</summary>
    public (List<StructureRepository.ResolvedRow> Resolved, List<StructureRepository.IssueRow> Issues) ComputeDerived(
        IReadOnlyCollection<ElementId>? pages, IReadOnlyCollection<ElementId>? properties)
    {
        var resolved = new List<StructureRepository.ResolvedRow>();
        var issues = new List<StructureRepository.IssueRow>();
        foreach (var page in LivePages(pages).OrderBy(p => p.ToInt64()))
        {
            foreach (var property in ResolvePage(page, properties).OrderBy(p => p.Definition.Id.ToInt64()))
            {
                var (origin, source) = property.Resolution switch
                {
                    Resolution<PropertyValue>.Own => ("own", (long?)null),
                    Resolution<PropertyValue>.Inherited inherited => ("inherited", inherited.Source.ToInt64()),
                    Resolution<PropertyValue>.FromDefinition => ("default", null),
                    _ => ("conflict", null),
                };
                if (property.Resolution.EffectiveValue is { } value)
                {
                    resolved.AddRange(ValueStorage.Rows(page, property.Definition.Id, value).Select(r => new StructureRepository.ResolvedRow(
                        r.OwnerId, r.PropertyId, r.Idx, r.VInt, r.VReal, r.VText, r.VRef, origin, source)));
                }
                else
                {
                    resolved.Add(new(page.ToInt64(), property.Definition.Id.ToInt64(), 0, null, null, null, null, origin, null));
                }

                foreach (var violation in property.Violations)
                {
                    var index = property.Definition.Rules.ToList().IndexOf(violation.Rule);
                    issues.Add(new(page.ToInt64(), property.Definition.Id.ToInt64(), index, ValidationRules.OpName(violation.Rule.Op),
                        ValidationRules.ToJson(violation.Rule).ToJsonString(), violation.Index));
                }
            }
        }

        return (resolved, issues);
    }

    /// <summary>Recomputes and writes the derived tables of the scope, in the caller's transaction (F08.R4).</summary>
    public void WriteDerived(
        SqliteConnection connection, SqliteTransaction transaction, IReadOnlyCollection<ElementId>? pages, IReadOnlyCollection<ElementId>? properties)
    {
        var (resolved, issues) = ComputeDerived(pages, properties);
        StructureRepository.ReplaceDerived(connection, transaction, pages, properties, resolved, issues);
    }

    // ─── IRuleContext ───────────────────────────────────────────────────────

    public ElementKind? KindOf(ElementId id) => Tree.Find(id)?.Kind;

    public bool IsInCategory(ElementId pageId, ElementId categoryId) =>
        CategoriesOf(pageId).Any(c => c == categoryId || Graph.IsAncestorOf(categoryId, c));

    private ValueLookup<PropertyValue> Lookup(ElementId property) =>
        (ElementId owner, [MaybeNullWhen(false)] out PropertyValue value) => _values.TryGetValue((owner, property), out value);
}
