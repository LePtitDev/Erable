namespace Erable.Core.Tree;

/// <summary>
/// Order of siblings: spaced integers. A new position takes the middle of the gap between its neighbours; when the
/// gap is exhausted (after ~16 insertions at the same place), the siblings are renumbered. Chosen over fractional
/// numbers, which silently lose precision, and over strings (fractional indexing), which are harder to inspect in SQL.
/// </summary>
public static class SortOrder
{
    /// <summary>Gap between two consecutive siblings after a renumbering.</summary>
    public const long Step = 1L << 16;

    /// <summary>A value strictly between the neighbours, or null when there is no room left (renumber).</summary>
    /// <param name="before">Sort order of the previous sibling, null when inserting first.</param>
    /// <param name="after">Sort order of the next sibling, null when inserting last.</param>
    public static long? Between(long? before, long? after)
    {
        switch (before, after)
        {
            case (null, null):
                return Step;
            case (null, { } next):
                return next > long.MinValue + Step ? next - Step : null;
            case ({ } previous, null):
                return previous < long.MaxValue - Step ? previous + Step : null;
            case ({ } previous, { } next):
                // Overflow-free midpoint.
                var middle = previous + ((next - previous) / 2);
                return middle > previous && middle < next ? middle : null;
        }
    }

    /// <summary>Evenly spaced values for <paramref name="count"/> siblings.</summary>
    public static IEnumerable<long> Renumber(int count) => Enumerable.Range(1, count).Select(i => i * Step);
}
