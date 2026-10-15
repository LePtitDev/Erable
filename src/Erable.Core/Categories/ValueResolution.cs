using System.Diagnostics.CodeAnalysis;

namespace Erable.Core.Categories;

/// <summary>Looks up the value an owner (page or category) defines itself for the property being resolved.</summary>
public delegate bool ValueLookup<T>(ElementId owner, [MaybeNullWhen(false)] out T value);

/// <summary>A category providing a value for a property, as candidate of a resolution.</summary>
public sealed record ValueCandidate<T>(ElementId CategoryId, T Value);

/// <summary>
/// Result of resolving a property value for a page or a category (docs/architecture.md §5.3, F07.R3). The value
/// itself is opaque: only its equality matters.
/// </summary>
public abstract record Resolution<T>
    where T : class
{
    private Resolution()
    {
    }

    /// <summary>The value is defined on the owner itself.</summary>
    public sealed record Own(T Value) : Resolution<T>;

    /// <summary>
    /// The value comes from a category (« via PNJ »). <see cref="Sources"/> lists the categories providing it, most
    /// specific first; several unrelated categories may agree on the same value without conflict.
    /// </summary>
    public sealed record Inherited(T Value, IReadOnlyList<ElementId> Sources) : Resolution<T>
    {
        public ElementId Source => Sources[0];
    }

    /// <summary>No category provides a value: the default of the property definition applies.</summary>
    public sealed record FromDefinition(T Value) : Resolution<T>;

    /// <summary>
    /// Unrelated categories provide different values: the page must fix the value itself (the UI never picks one).
    /// Candidates are in the order of the page's categories.
    /// </summary>
    public sealed record Conflict(IReadOnlyList<ValueCandidate<T>> Candidates) : Resolution<T>;

    /// <summary>Nothing defines a value.</summary>
    public sealed record Unset : Resolution<T>;

    /// <summary>The effective value; null for a conflict or when unset.</summary>
    public T? EffectiveValue => this switch
    {
        Own own => own.Value,
        Inherited inherited => inherited.Value,
        FromDefinition fromDefinition => fromDefinition.Value,
        Conflict or Unset => null,
        _ => throw new InvalidOperationException("Unknown resolution."),
    };
}

/// <summary>
/// Resolution of property values through category inheritance (docs/architecture.md §5.3, F07.R3):
/// <list type="number">
/// <item>the value defined on the page;</item>
/// <item>otherwise the value of the most specific category: for each category of the page, the nearest category of
/// its chain (itself first) that defines a value is a candidate; a candidate that is an ancestor of another candidate
/// is overridden by it (PNJ + Marchand: Marchand wins, no conflict). Candidates left with different values are in
/// conflict; candidates agreeing on the same value are not (decision M2-01);</item>
/// <item>otherwise the default of the property definition.</item>
/// </list>
/// The result does not depend on the order in which the page declares its categories (only the order of the
/// conflict candidates, a display concern, follows it).
/// </summary>
public static class ValueResolver
{
    public static Resolution<T> ForPage<T>(
        CategoryGraph graph, ElementId pageId, IReadOnlyList<ElementId> pageCategories, ValueLookup<T> values,
        T? definitionDefault, IEqualityComparer<T>? comparer = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pageCategories);
        ArgumentNullException.ThrowIfNull(values);
        if (values(pageId, out var own))
        {
            return new Resolution<T>.Own(own);
        }

        // Nearest provider of each chain, in the order of the page's categories (deleted categories are skipped).
        var candidates = new List<ValueCandidate<T>>();
        foreach (var category in pageCategories.Where(graph.Contains))
        {
            if (NearestProvider(graph, category, values) is { } candidate && candidates.All(c => c.CategoryId != candidate.CategoryId))
            {
                candidates.Add(candidate);
            }
        }

        // A provider overridden by a more specific provider of its own chain does not count.
        var providers = candidates.Select(c => c.CategoryId).ToArray();
        candidates.RemoveAll(c => providers.Any(other => graph.IsAncestorOf(c.CategoryId, other)));
        if (candidates.Count == 0)
        {
            return definitionDefault is null ? new Resolution<T>.Unset() : new Resolution<T>.FromDefinition(definitionDefault);
        }

        var equality = comparer ?? EqualityComparer<T>.Default;
        var first = candidates[0].Value;
        if (candidates.All(c => equality.Equals(c.Value, first)))
        {
            var sources = candidates
                .Select((c, index) => (c.CategoryId, Depth: graph.Depth(c.CategoryId), index))
                .OrderByDescending(s => s.Depth).ThenBy(s => s.index)
                .Select(s => s.CategoryId)
                .ToArray();
            return new Resolution<T>.Inherited(first, sources);
        }

        return new Resolution<T>.Conflict(candidates);
    }

    /// <summary>Value of a property for a category: its own, else its nearest ancestor's, else the definition default.</summary>
    public static Resolution<T> ForCategory<T>(CategoryGraph graph, ElementId categoryId, ValueLookup<T> values, T? definitionDefault)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(values);
        if (values(categoryId, out var own))
        {
            return new Resolution<T>.Own(own);
        }

        foreach (var ancestor in graph.Ancestors(categoryId))
        {
            if (values(ancestor, out var inherited))
            {
                return new Resolution<T>.Inherited(inherited, [ancestor]);
            }
        }

        return definitionDefault is null ? new Resolution<T>.Unset() : new Resolution<T>.FromDefinition(definitionDefault);
    }

    private static ValueCandidate<T>? NearestProvider<T>(CategoryGraph graph, ElementId category, ValueLookup<T> values)
        where T : class
    {
        foreach (var id in (IEnumerable<ElementId>)[category, .. graph.Ancestors(category)])
        {
            if (values(id, out var value))
            {
                return new ValueCandidate<T>(id, value);
            }
        }

        return null;
    }
}

/// <summary>A page listed under a category, and through which of its categories (« Ilsa (via Marchand) », E2E-07-07).</summary>
/// <param name="PageId">The page.</param>
/// <param name="ViaCategoryId">The most specific category of the page within the listed subtree; equal to the listed category for a direct member.</param>
public sealed record CategoryMember(ElementId PageId, ElementId ViaCategoryId);

/// <summary>Pages of a category, with or without its sub-categories (F07.R4).</summary>
public static class CategoryMembership
{
    public static IReadOnlyList<CategoryMember> PagesOf(
        CategoryGraph graph, ElementId categoryId, bool includeSubCategories,
        IEnumerable<(ElementId PageId, IReadOnlyList<ElementId> Categories)> pages)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(pages);
        var scope = new HashSet<ElementId> { categoryId };
        if (includeSubCategories)
        {
            scope.UnionWith(graph.Descendants(categoryId));
        }

        var result = new List<CategoryMember>();
        foreach (var (pageId, categories) in pages)
        {
            // The deepest matching category, the first declared one on ties.
            var via = categories
                .Where(scope.Contains)
                .Select((c, index) => (Id: c, Depth: graph.Depth(c), index))
                .OrderByDescending(c => c.Depth).ThenBy(c => c.index)
                .Select(c => c.Id)
                .FirstOrDefault();
            if (!via.IsNone)
            {
                result.Add(new CategoryMember(pageId, via));
            }
        }

        return result;
    }
}
