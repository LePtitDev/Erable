using System.Text.Json.Nodes;

namespace Erable.Core.Merge;

/// <summary>A property whose value both editors changed, differently.</summary>
public sealed record ValueConflict(string PropertyId, JsonNode? Base, JsonNode? Mine, JsonNode? Theirs);

/// <summary>
/// The own values of a page after a three-way merge. <see cref="Merged"/> holds, for a conflicting property, the value
/// of the other editor (<c>theirs</c>): it is only a placeholder, a merge with conflicts is never saved.
/// </summary>
public sealed record ValuesMerge(IReadOnlyDictionary<string, JsonNode?> Merged, IReadOnlyList<ValueConflict> Conflicts);

/// <summary>
/// Field-by-field three-way merge of what a page holds besides its text: its categories and its property values
/// (M4-05, F14.R5). A field only one editor changed takes that change; a field both changed the same way keeps it; only
/// a field both changed differently is a conflict.
/// </summary>
public static class FieldMerge
{
    /// <summary>
    /// A list treated as a set (the categories of a page): what <c>mine</c> added is added to <c>theirs</c>, what
    /// <c>mine</c> removed is removed from it. The order of <c>theirs</c> is kept, additions come last. Never conflicts:
    /// two editors adding or removing different items both get their way.
    /// </summary>
    public static IReadOnlyList<T> MergeSet<T>(IReadOnlyList<T> baseSet, IReadOnlyList<T> mine, IReadOnlyList<T> theirs)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(baseSet);
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);
        var removed = baseSet.Except(mine).ToHashSet();
        var merged = theirs.Where(item => !removed.Contains(item)).Distinct().ToList();
        foreach (var added in mine.Except(baseSet))
        {
            if (!merged.Contains(added))
            {
                merged.Add(added);
            }
        }

        return merged;
    }

    /// <summary>The property values of a page (property ID → JSON value; a missing key is « no value »).</summary>
    public static ValuesMerge MergeValues(
        IReadOnlyDictionary<string, JsonNode?> baseValues,
        IReadOnlyDictionary<string, JsonNode?> mine,
        IReadOnlyDictionary<string, JsonNode?> theirs)
    {
        ArgumentNullException.ThrowIfNull(baseValues);
        ArgumentNullException.ThrowIfNull(mine);
        ArgumentNullException.ThrowIfNull(theirs);
        var merged = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        var conflicts = new List<ValueConflict>();
        foreach (var key in baseValues.Keys.Union(mine.Keys).Union(theirs.Keys).Order(StringComparer.Ordinal))
        {
            var b = baseValues.GetValueOrDefault(key);
            var m = mine.GetValueOrDefault(key);
            var t = theirs.GetValueOrDefault(key);
            JsonNode? chosen;
            if (JsonNode.DeepEquals(m, b) || JsonNode.DeepEquals(m, t))
            {
                chosen = t;
            }
            else if (JsonNode.DeepEquals(t, b))
            {
                chosen = m;
            }
            else
            {
                chosen = t;
                conflicts.Add(new ValueConflict(key, b?.DeepClone(), m?.DeepClone(), t?.DeepClone()));
            }

            if (chosen is not null)
            {
                merged[key] = chosen.DeepClone();
            }
        }

        return new ValuesMerge(merged, conflicts);
    }
}
