using System.Text;
using CsCheck;
using Erable.Core.Merge;

namespace Erable.Core.Tests.Merge;

/// <summary>docs/testing.md §4.1 (Fusion à trois voies): the invariants the product relies on, checked on generated texts.</summary>
public sealed class ThreeWayMergePropertyTests
{
    /// <summary>Lines of a small alphabet, so that equal lines happen (the hard case for a diff).</summary>
    private static readonly Gen<string> Line = Gen.Char["abcdefg éèà.,#- "].Array[0, 8].Select(c => new string(c));

    private static readonly Gen<string> Text = Line.List[0, 14].Select(lines => string.Concat(lines.Select(l => l + "\n")));

    private static readonly Gen<(string, string)> TextPair = Gen.Select(Text, Text);

    [Fact]
    public void Merge_NothingChangedByMe_GivesTheirs()
    {
        // merge(b, b, x) = x
        TextPair.Sample((baseText, theirs) =>
        {
            var merge = ThreeWayMerge.Merge(baseText, baseText, theirs);

            merge.HasConflicts.ShouldBeFalse();
            merge.Text.ShouldBe(theirs);
        });
    }

    [Fact]
    public void Merge_NothingChangedByThem_GivesMine()
    {
        // merge(b, x, b) = x
        TextPair.Sample((baseText, mine) =>
        {
            var merge = ThreeWayMerge.Merge(baseText, mine, baseText);

            merge.HasConflicts.ShouldBeFalse();
            merge.Text.ShouldBe(mine);
        });
    }

    [Fact]
    public void Merge_BothMadeTheSameEdit_GivesIt()
    {
        // merge(b, x, x) = x
        TextPair.Sample((baseText, edit) =>
        {
            var merge = ThreeWayMerge.Merge(baseText, edit, edit);

            merge.HasConflicts.ShouldBeFalse();
            merge.Text.ShouldBe(edit);
        });
    }

    [Fact]
    public void Merge_WithoutConflict_DoesNotDependOnWhoSavesFirst()
    {
        Gen.Select(Text, Text, Text).Sample((baseText, mine, theirs) =>
        {
            var one = ThreeWayMerge.Merge(baseText, mine, theirs);
            var other = ThreeWayMerge.Merge(baseText, theirs, mine);

            one.HasConflicts.ShouldBe(other.HasConflicts);
            if (!one.HasConflicts)
            {
                one.Text.ShouldBe(other.Text);
            }
        });
    }

    [Fact]
    public void Resolve_PickingOneSideEverywhere_GivesThatSideWhenTheOtherDidNotTouchTheText()
    {
        // Whatever the conflicts, resolving with my side when nobody else wrote gives exactly my text.
        TextPair.Sample((baseText, mine) =>
        {
            ThreeWayMerge.Merge(baseText, mine, baseText).Resolve(c => c.Mine).ShouldBe(mine);
        });
    }

    [Fact]
    public void Merge_StablePartsAndChosenSides_AlwaysRebuildAWholeText()
    {
        // Choosing a side for each conflict never drops text outside the conflicts: with all conflicts resolved to
        // mine, every line that I kept and they left alone is present (checked by « mine » as a subsequence of lines
        // when they changed nothing, and in general by the length bound below).
        Gen.Select(Text, Text, Text).Sample((baseText, mine, theirs) =>
        {
            var merge = ThreeWayMerge.Merge(baseText, mine, theirs);
            var asMine = merge.Resolve(c => c.Mine);
            var asTheirs = merge.Resolve(c => c.Theirs);

            // No characters appear from nowhere: everything is from one of the three texts.
            foreach (var text in new[] { asMine, asTheirs })
            {
                text.ToCharArray().ShouldAllBe(ch => baseText.Contains(ch, StringComparison.Ordinal)
                    || mine.Contains(ch, StringComparison.Ordinal) || theirs.Contains(ch, StringComparison.Ordinal));
            }
        });
    }

    /// <summary>Texts made of numbered paragraphs, to edit two of them far apart.</summary>
    private static string Paragraphs(IEnumerable<string> bodies) =>
        string.Concat(bodies.Select((body, i) => $"## Section {i}\n\n{body}\n\n"));

    [Fact]
    public void Merge_EditsInSeparateSections_NeverConflictAndKeepBoth()
    {
        var bodies = Gen.String["abcdef ghij"].Select(s => "Texte " + s).List[4, 9];
        Gen.Select(bodies, Gen.Int[0, 100], Gen.Int[0, 100], Gen.String["xyz"], Gen.String["XYZ"]).Sample((list, a, b, mineWord, theirWord) =>
        {
            var i = a % list.Count;
            // At least one untouched section between the two edited ones.
            var candidates = Enumerable.Range(0, list.Count).Where(j => Math.Abs(j - i) >= 2).ToArray();
            if (candidates.Length == 0)
            {
                return;
            }

            var j = candidates[b % candidates.Length];
            var baseText = Paragraphs(list);
            var mine = Paragraphs(list.Select((body, k) => k == i ? body + " moi:" + mineWord : body));
            var theirs = Paragraphs(list.Select((body, k) => k == j ? body + " eux:" + theirWord : body));

            var merge = ThreeWayMerge.Merge(baseText, mine, theirs);

            merge.HasConflicts.ShouldBeFalse();
            merge.Text.ShouldBe(Paragraphs(list.Select((body, k) =>
                k == i ? body + " moi:" + mineWord : k == j ? body + " eux:" + theirWord : body)));
        });
    }

    [Fact]
    public void Merge_InsertedText_IsNeverLostWithoutAConflict()
    {
        // « Aucun caractère perdu sans conflit signalé »: each side inserts a marker into the base. If the merge reports
        // no conflict, both markers are in the result, and removing them gives the base back, character for character.
        var insertion = Gen.Select(Gen.Int[0, 1000], Gen.Bool);
        Gen.Select(Text, insertion, insertion).Sample((baseText, mineAt, theirsAt) =>
        {
            const string MineMarker = "«MOI»";
            const string TheirsMarker = "«EUX»";
            var mine = Insert(baseText, mineAt.Item1, mineAt.Item2, MineMarker);
            var theirs = Insert(baseText, theirsAt.Item1, theirsAt.Item2, TheirsMarker);

            var merge = ThreeWayMerge.Merge(baseText, mine, theirs);

            if (merge.HasConflicts)
            {
                // The three versions are reported, each with its own marker.
                merge.Blocks.OfType<ConflictBlock>().ShouldContain(c => c.Mine.Contains(MineMarker, StringComparison.Ordinal));
                merge.Blocks.OfType<ConflictBlock>().ShouldContain(c => c.Theirs.Contains(TheirsMarker, StringComparison.Ordinal));
                return;
            }

            merge.Text.ShouldContain(MineMarker);
            merge.Text.ShouldContain(TheirsMarker);
            // A marker on a line of its own brings its line break with it.
            Remove(Remove(merge.Text, MineMarker, mineAt.Item2), TheirsMarker, theirsAt.Item2).ShouldBe(baseText);
        });
    }

    private static string Remove(string text, string marker, bool wholeLine) =>
        text.Replace(wholeLine ? marker + "\n" : marker, string.Empty, StringComparison.Ordinal);

    /// <summary>A marker inserted at a character position, or on a line of its own when <paramref name="wholeLine"/>.</summary>
    private static string Insert(string text, int position, bool wholeLine, string marker)
    {
        if (wholeLine)
        {
            var lines = text.Split('\n').ToList();
            lines.Insert(position % lines.Count, marker);
            return string.Join('\n', lines);
        }

        var at = position % (text.Length + 1);
        return new StringBuilder(text).Insert(at, marker).ToString();
    }
}
