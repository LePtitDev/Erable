using System.Diagnostics;
using System.Text;
using CsCheck;
using Erable.Core.Markdown;

namespace Erable.Core.Tests.Markdown;

/// <summary>docs/testing.md §4.1 (Parseur de références).</summary>
public sealed partial class MarkdownPropertyTests
{
    /// <summary>
    /// Plain prose: letters, digits, single spaces, line breaks and harmless punctuation. Runs of spaces are collapsed:
    /// four leading spaces would start an indented code block, where references are (rightly) not recognized.
    /// </summary>
    private static readonly Gen<string> Prose = Gen.Char["abcdefghijklmnopqrstuvwxyzéèàçABCXYZ0123456789 ,.;:'?\n"].Array[0, 40]
        .Select(chars => MultipleSpaces().Replace(new string(chars), " "));

    private static readonly Gen<ElementId> Ids = Gen.ULong[ElementId.FirstRandomValue, ulong.MaxValue].Select(v => new ElementId(v));

    private static readonly Gen<string> Labels = Gen.Char["abcdefghij XYZéà|#"].Array[1, 12].Select(chars => new string(chars))
        .Where(l => l.Trim().Length > 0);

    private sealed record Piece(ElementId Id, string? Label, bool Embed, string Before);

    [Fact]
    public void Analyze_GeneratedReferencesInProse_AreFoundAtTheirPosition()
    {
        var pieces = Gen.Select(Ids, Labels.Null(), Gen.Bool, Prose, (id, label, embed, before) => new Piece(id, label, embed, before));
        Gen.Select(pieces.List[1, 8], Prose).Sample((list, tail) =>
        {
            var markdown = new StringBuilder();
            var expected = new List<(ElementId, string?, bool, int, int)>();
            foreach (var piece in list)
            {
                // A space on each side: a reference glued to prose letters is still a reference, but keep the
                // generated text from forming other syntax with the brackets.
                markdown.Append(piece.Before).Append(' ');
                var token = (piece.Embed ? "!" : "") + "[[" + piece.Id + (piece.Label is null ? "" : "|" + piece.Label) + "]]";
                expected.Add((piece.Id, piece.Label?.Trim(), piece.Embed, markdown.Length, markdown.Length + token.Length));
                markdown.Append(token).Append(' ');
            }

            markdown.Append(tail);
            var actual = ErableMarkdown.Analyze(markdown.ToString()).References
                .Select(r => (r.Id, r.Label, r.IsEmbed, r.Start, r.End)).ToList();

            actual.ShouldBe(expected);
        });
    }

    [Fact]
    public void Analyze_RandomInput_NeverThrows()
    {
        var markdownish = Gen.Char["[]!|#{}()<>`*_~\\-=:/ \n\r\taZ03F9é😀"].Array[0, 200].Select(c => new string(c));
        Gen.OneOf(markdownish, Gen.String).Sample(input =>
        {
            var analysis = ErableMarkdown.Analyze(input);
            analysis.References.ShouldAllBe(r => r.Start >= 0 && r.End <= input.Length && r.Start < r.End);
        });
    }

    [Fact]
    public void Analyze_OneMegabyte_IsLinear()
    {
        static string Page(int repeat)
        {
            var block = $"## Titre\n\nTexte [[3F2A9C14E07B5D21|lien]] et ![[7A1C4E90B3F2D615]]{{width=320}} [[[[ ]] ` x `\n\n- [ ] tâche\n\n| a | b |\n|---|---|\n| [[5B19C0E2A4D7F311]] | c |\n\n";
            return string.Concat(Enumerable.Repeat(block, repeat));
        }

        var small = Page(1_000);
        var large = Page(8_000); // ≈ 1 MB
        ErableMarkdown.Analyze(small); // warm-up

        var timeSmall = Measure(() => ErableMarkdown.Analyze(small));
        var timeLarge = Measure(() => ErableMarkdown.Analyze(large));

        large.Length.ShouldBeGreaterThan(1_000_000);
        ErableMarkdown.Analyze(large).References.Count.ShouldBe(3 * 8_000);
        // 8× the input must take far less than 64× the time (a quadratic parser); allow noise up to 24×.
        timeLarge.TotalMilliseconds.ShouldBeLessThan(Math.Max(timeSmall.TotalMilliseconds, 5) * 24);
    }

    [System.Text.RegularExpressions.GeneratedRegex(" {2,}")]
    private static partial System.Text.RegularExpressions.Regex MultipleSpaces();

    private static TimeSpan Measure(Action action)
    {
        var best = TimeSpan.MaxValue;
        for (var i = 0; i < 3; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            best = TimeSpan.FromTicks(Math.Min(best.Ticks, stopwatch.Elapsed.Ticks));
        }

        return best;
    }
}
