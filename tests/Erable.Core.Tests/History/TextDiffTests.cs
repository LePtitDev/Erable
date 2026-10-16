using CsCheck;
using Erable.Core.History;

namespace Erable.Core.Tests.History;

/// <summary>Diff of the text of two revisions (F13.R2, M3-05).</summary>
public sealed class TextDiffTests
{
    private static readonly Gen<string> GenLine = Gen.String[Gen.Char["abc de"], 1, 12];

    private static string[] Side(IReadOnlyList<DiffRow> rows, Func<DiffRow, DiffLine?> side) =>
        [.. rows.Select(side).OfType<DiffLine>().Select(l => l.Text)];

    [Fact]
    public void Compare_SameText_IsUnchanged()
    {
        var rows = TextDiff.Compare("a\nb", "a\nb");

        rows.ShouldAllBe(r => r.Kind == DiffRowKind.Unchanged);
        rows.Count.ShouldBe(2);
    }

    [Fact]
    public void Compare_AddedAndRemovedLines_AreMarked()
    {
        var rows = TextDiff.Compare("un\ndeux\ntrois", "un\ntrois\nquatre");

        rows.Select(r => r.Kind).ShouldBe([DiffRowKind.Unchanged, DiffRowKind.Removed, DiffRowKind.Unchanged, DiffRowKind.Added]);
        rows[1].Old!.Text.ShouldBe("deux");
        rows[1].New.ShouldBeNull();
        rows[3].New!.Text.ShouldBe("quatre");
        rows[3].Old.ShouldBeNull();
    }

    [Fact]
    public void Compare_ModifiedLine_MarksTheChangedWords()
    {
        var row = TextDiff.Compare("Ilsa vend des cartes", "Ilsa vend des cartes anciennes").Single();

        row.Kind.ShouldBe(DiffRowKind.Modified);
        row.New!.Pieces.Where(p => p.Changed).Select(p => p.Text.Trim()).ShouldContain("anciennes");
        row.Old!.Pieces.Where(p => p.Changed).ShouldBeEmpty();
        string.Concat(row.New.Pieces.Select(p => p.Text)).ShouldBe("Ilsa vend des cartes anciennes");
    }

    [Fact]
    public void Compare_NullAndEmpty_AreAccepted()
    {
        TextDiff.Compare(null, "a").Select(r => r.Kind).ShouldContain(DiffRowKind.Added);
        TextDiff.Compare("a", null).Select(r => r.Kind).ShouldContain(DiffRowKind.Removed);
    }

    [Fact]
    public void Compare_AnyTwoTexts_RowsRebuildBothTexts()
    {
        Gen.Select(GenLine.List[1, 8], GenLine.List[1, 8]).Sample((a, b) =>
        {
            var rows = TextDiff.Compare(string.Join('\n', a), string.Join('\n', b));

            Side(rows, r => r.Old).ShouldBe(a);
            Side(rows, r => r.New).ShouldBe(b);
            rows.Where(r => r.Kind == DiffRowKind.Unchanged).ShouldAllBe(r => r.Old!.Text == r.New!.Text);
        });
    }

    [Fact]
    public void Compare_LineNumbers_StartAtOneAndFollowEachVersion()
    {
        var rows = TextDiff.Compare("a\nb\nc", "a\nc\nd");

        rows.Select(r => r.Old?.Number).OfType<int>().ShouldBe([1, 2, 3]);
        rows.Select(r => r.New?.Number).OfType<int>().ShouldBe([1, 2, 3]);
    }
}
