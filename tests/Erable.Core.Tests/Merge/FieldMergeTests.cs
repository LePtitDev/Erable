using System.Text.Json.Nodes;
using CsCheck;
using Erable.Core.Merge;

namespace Erable.Core.Tests.Merge;

public sealed class FieldMergeTests
{
    private static JsonNode? Json(string? text) => text is null ? null : JsonNode.Parse(text);

    private static Dictionary<string, JsonNode?> Values(params (string Key, string? Json)[] entries) =>
        entries.ToDictionary(e => e.Key, e => FieldMergeTests.Json(e.Json), StringComparer.Ordinal);

    [Fact]
    public void MergeSet_AdditionsAndRemovalsOfBothSides_AreCombined()
    {
        var merged = FieldMerge.MergeSet(["a", "b", "c"], ["a", "b", "c", "mine"], ["b", "c", "d"]);

        merged.ShouldBe(["b", "c", "d", "mine"]);
    }

    [Fact]
    public void MergeSet_IGotRidOfWhatTheyKept_Removes()
    {
        FieldMerge.MergeSet(["a", "b"], ["a"], ["a", "b", "c"]).ShouldBe(["a", "c"]);
    }

    [Fact]
    public void MergeSet_BothAddedTheSameItem_KeepsItOnce()
    {
        FieldMerge.MergeSet<string>([], ["x"], ["x"]).ShouldBe(["x"]);
    }

    [Fact]
    public void MergeSet_Properties()
    {
        var items = Gen.Int[0, 6].List[0, 6].Select(l => l.Distinct().ToList());
        Gen.Select(items, items, items).Sample((b, m, t) =>
        {
            // Nothing changed by me: theirs. Nothing changed by them: mine (as a set). Both are sets: no duplicates.
            FieldMerge.MergeSet(b, b, t).ShouldBe(t);
            FieldMerge.MergeSet(b, m, b).OrderBy(x => x).ShouldBe(m.OrderBy(x => x));
            var merged = FieldMerge.MergeSet(b, m, t);
            merged.Distinct().Count().ShouldBe(merged.Count);
            // What I added is there, what I removed is not.
            m.Except(b).ShouldAllBe(x => merged.Contains(x));
            b.Except(m).ShouldAllBe(x => !merged.Contains(x));
        });
    }

    [Fact]
    public void MergeValues_FieldsChangedByDifferentSides_AreBothKept()
    {
        var merge = FieldMerge.MergeValues(
            Values(("niveau", "10"), ("role", "\"Garde\"")),
            Values(("niveau", "12"), ("role", "\"Garde\"")),
            Values(("niveau", "10"), ("role", "\"Marchande\"")));

        merge.Conflicts.ShouldBeEmpty();
        merge.Merged["niveau"]!.ToJsonString().ShouldBe("12");
        merge.Merged["role"]!.ToJsonString().ShouldBe("\"Marchande\"");
    }

    [Fact]
    public void MergeValues_SameFieldChangedDifferently_IsAConflict()
    {
        var merge = FieldMerge.MergeValues(Values(("niveau", "10")), Values(("niveau", "12")), Values(("niveau", "14")));

        var conflict = merge.Conflicts.ShouldHaveSingleItem();
        conflict.PropertyId.ShouldBe("niveau");
        conflict.Base!.ToJsonString().ShouldBe("10");
        conflict.Mine!.ToJsonString().ShouldBe("12");
        conflict.Theirs!.ToJsonString().ShouldBe("14");
    }

    [Fact]
    public void MergeValues_SameChangeOnBothSides_IsNotAConflict()
    {
        var merge = FieldMerge.MergeValues(Values(("niveau", "10")), Values(("niveau", "12")), Values(("niveau", "12")));

        merge.Conflicts.ShouldBeEmpty();
        merge.Merged["niveau"]!.ToJsonString().ShouldBe("12");
    }

    [Fact]
    public void MergeValues_ValueAddedByOneSideAndRemovedByTheOther_IsAConflictOnlyIfTheOtherChangedIt()
    {
        // I removed it, they did not touch it: removed.
        var removed = FieldMerge.MergeValues(Values(("niveau", "10")), Values(), Values(("niveau", "10")));
        removed.Conflicts.ShouldBeEmpty();
        removed.Merged.ShouldNotContainKey("niveau");

        // I removed it, they changed it: conflict.
        var clash = FieldMerge.MergeValues(Values(("niveau", "10")), Values(), Values(("niveau", "14")));
        clash.Conflicts.ShouldHaveSingleItem();
    }

    [Fact]
    public void MergeValues_NothingChangedByMe_GivesTheirs()
    {
        var baseValues = Values(("a", "1"), ("b", "\"x\""));
        var theirs = Values(("a", "2"), ("c", "[1,2]"));

        var merge = FieldMerge.MergeValues(baseValues, baseValues, theirs);

        merge.Conflicts.ShouldBeEmpty();
        merge.Merged.Keys.Order().ShouldBe(["a", "c"]);
    }
}
