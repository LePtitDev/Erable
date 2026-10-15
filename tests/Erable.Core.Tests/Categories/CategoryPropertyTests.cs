using System.Diagnostics.CodeAnalysis;
using CsCheck;
using Erable.Core.Categories;

namespace Erable.Core.Tests.Categories;

/// <summary>docs/testing.md §4.1 (Héritage de catégories).</summary>
public sealed class CategoryPropertyTests
{
    private const int Size = 12;

    private static ElementId Id(int index) => new(0x1000 + (ulong)index);

    /// <summary>A forest: category i gets a parent among 0..i-1, or none (encoded as a negative value).</summary>
    private static readonly Gen<CategoryGraph> GenForest =
        Gen.Int[-Size, Size].Array[Size].Select(parents => CategoryGraph.Load(
            parents.Select((p, i) => new CategoryNode(Id(i), p >= 0 && i > 0 ? Id(p % i) : ElementId.None))));

    [Fact]
    public void AnySequenceOfReparentings_NeverCreatesACycle()
    {
        Gen.Select(GenForest, Gen.Select(Gen.Int[0, Size - 1], Gen.Int[-1, Size - 1]).Array[1, 60]).Sample((graph, moves) =>
        {
            foreach (var (subject, target) in moves)
            {
                var parent = target < 0 ? ElementId.None : Id(target);
                var wouldLoop = !parent.IsNone && (parent == Id(subject) || NaiveAncestors(graph, parent).Contains(Id(subject)));

                var result = graph.SetParent(Id(subject), parent);

                result.IsSuccess.ShouldBe(!wouldLoop);
                graph.FindCycles().ShouldBeEmpty();
                if (result.IsSuccess)
                {
                    graph.ParentOf(Id(subject)).ShouldBe(parent);
                }
            }
        });
    }

    [Fact]
    public void ForPage_Resolution_DoesNotDependOnTheOrderOfTheCategories()
    {
        var genValues = Gen.Int[-1, 2].Array[Size];
        var genCategories = Gen.Int[0, Size - 1].Array[0, 5];
        Gen.Select(GenForest, genValues, genCategories, Gen.Int).Sample((graph, values, categories, seed) =>
        {
            ValueLookup<string> lookup = (ElementId owner, [MaybeNullWhen(false)] out string value) =>
            {
                var index = (int)(owner.Value - 0x1000);
                var found = index is >= 0 and < Size && values[index] >= 0;
                value = found ? values[index].ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
                return found;
            };
            var ordered = categories.Select(Id).ToArray();
            var random = new Random(seed);
            var shuffled = ordered.OrderBy(_ => random.Next()).ToArray();

            var a = ValueResolver.ForPage(graph, new ElementId(1), ordered, lookup, "d");
            var b = ValueResolver.ForPage(graph, new ElementId(1), shuffled, lookup, "d");

            Normalize(a).ShouldBe(Normalize(b));
        });
    }

    /// <summary>Kind, value and the set of categories involved: everything but the display order.</summary>
    private static string Normalize(Resolution<string> resolution) => resolution switch
    {
        Resolution<string>.Inherited i => $"inherited:{i.Value}:{string.Join(',', i.Sources.Select(s => s.Value).Order())}",
        Resolution<string>.Conflict c => $"conflict:{string.Join(',', c.Candidates.Select(x => $"{x.CategoryId.Value}={x.Value}").Order(StringComparer.Ordinal))}",
        _ => resolution.ToString(),
    };

    private static List<ElementId> NaiveAncestors(CategoryGraph graph, ElementId id)
    {
        var result = new List<ElementId>();
        for (var current = graph.ParentOf(id); !current.IsNone && result.Count <= Size; current = graph.ParentOf(current))
        {
            result.Add(current);
        }

        return result;
    }
}
