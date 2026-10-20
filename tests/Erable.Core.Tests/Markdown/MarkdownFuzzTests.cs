using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Erable.Core.Markdown;

namespace Erable.Core.Tests.Markdown;

/// <summary>
/// Fuzzing of the Markdown analysis (docs/testing.md §12): random inputs built from the tokens that matter to
/// the grammar must never throw, never hang, and cost time linear in the size of the input. A short run belongs to the
/// regular suite; the weekly workflow sets <c>ERABLE_FUZZ_SECONDS=600</c>. A failing input is written to
/// <c>artifacts/fuzz/</c> (or <c>ERABLE_FUZZ_OUTPUT</c>) to be added to <c>spec/fixtures/markdown/edge-cases.json</c>.
/// </summary>
public sealed class MarkdownFuzzTests
{
    public const string SecondsVariable = "ERABLE_FUZZ_SECONDS";

    private static readonly TimeSpan PerInputLimit = TimeSpan.FromSeconds(5);

    // No tab: inside a list item, Markdig's precise source locations drift by the width of the tab (« * ##<TAB>#    [[ID]] »
    // reports the reference two characters early, R10). Upstream behaviour; remove this
    // restriction when it is fixed.
    private static readonly string[] Tokens =
    [
        "[[", "]]", "![[", "[", "]", "(", ")", "![", "](", "|", "#", "##", "###### ", "# ", "\n", "\n\n", "\r\n", " ", "  ", "    ",
        "`", "``", "```", "~~~", "*", "**", "_", "__", "~~", "~", ">", "> ", "- ", "* ", "1. ", "- [ ] ", "- [x] ",
        "<", ">", "<script>", "<!--", "&amp;", "&#x0;", "\\", "\\[", "|", "---", "| a | b |", "|---|---|", "{#", "}", "{width=", "{",
        "3F2A9C14E07B5D21", "0000000000000000", "3F2A9C14E07B5D2", "ABCDEFGHIJKLMNOP", "#section", "#", "é", "—", "日本語", "😀",
        "\ud83d", "\u0000", "‮", "http://example.org/", "[[3F2A9C14E07B5D21]]", "![[3F2A9C14E07B5D21|label]]", "[[3F2A9C14E07B5D21#h|a]]",
    ];

    /// <summary>Strings that make a naive parser quadratic: a token repeated, then a closing that never matches.</summary>
    private static readonly string[] Pathological =
    [
        "[[", "![[", "]", "`", "*", "_", "- ", "<", "[[3F2A9C14E07B5D21|", "\\", "~", "|", "# ",
    ];

    /// <summary>
    /// Found by this fuzzing: Markdig takes quadratic time on these before it gives up on the nesting depth
    /// (« [a]( » × 16 000 = 64 KB takes about a second, × 4 for each doubling). Not fixed upstream: the limits of
    /// <see cref="MarkdownLimits"/> refuse them before the parser runs (docs/markdown-syntax.md §6).
    /// </summary>
    private static readonly string[] Guarded = ["[a](", "> ", "[", "1. ", "]"];

    [Fact]
    public void Analyze_RandomInputs_NeverThrowsAndStaysCoherent()
    {
        var budget = Budget();
        var random = new Random(Seed());
        var clock = Stopwatch.StartNew();
        var inputs = 0;
        do
        {
            var markdown = RandomInput(random);
            Check(markdown);
            inputs++;
        }
        while (clock.Elapsed < budget);

        inputs.ShouldBeGreaterThan(0);
    }

    /// <summary>Regression of a stack overflow found by the fuzzing: the text of nested inlines was extracted recursively.</summary>
    [Fact]
    public void Analyze_ThousandsOfNestedInlines_DoesNotOverflowTheStack()
    {
        var analysis = ErableMarkdown.Analyze(string.Concat(Enumerable.Repeat("[[", 5_000)));

        analysis.References.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_NestingBeyondTheParserLimit_KeepsTheTextAsPlainText()
    {
        var markdown = string.Concat(Enumerable.Repeat("> ", 20_000)) + "texte " + "[[3F2A9C14E07B5D21]]";

        var analysis = ErableMarkdown.Analyze(markdown);

        analysis.PlainText.ShouldContain("texte");
        analysis.Headings.ShouldBeEmpty();
    }

    [Fact]
    public void Analyze_PathologicalRepetitions_TakeTimeLinearInTheSize() => AssertLinear(Pathological);

    [Fact]
    public void Check_RepetitionsThatAreQuadraticInTheParser_AreRefused()
    {
        foreach (var token in Guarded)
        {
            MarkdownLimits.Check(string.Concat(Enumerable.Repeat(token, 40_000))).ShouldNotBeNull(token);
            MarkdownLimits.Check(string.Concat(Enumerable.Repeat(token, 8_000))).ShouldNotBeNull(token);
        }
    }

    private static void AssertLinear(string[] tokens)
    {
        const int Small = 5_000;
        const int Large = 8 * Small;
        foreach (var token in tokens)
        {
            // Warm up (JIT, pipeline), then compare the best of three for each size.
            ElapsedFor(token, 1_000);
            var small = Best(token, Small);
            var large = Best(token, Large);
            // Linear: 8× the size, about 8× the time. Quadratic would be 64×. The floor keeps timer noise out.
            var allowed = Math.Max(small.TotalMilliseconds * 8 * 4, 250);
            large.TotalMilliseconds.ShouldBeLessThan(allowed, $"{Show(token)} × {Large}: {large.TotalMilliseconds:F0} ms vs {small.TotalMilliseconds:F0} ms for × {Small}");
        }
    }

    private static TimeSpan Best(string token, int repetitions) =>
        Enumerable.Range(0, 3).Select(_ => ElapsedFor(token, repetitions)).Min();

    private static TimeSpan ElapsedFor(string token, int repetitions)
    {
        var markdown = string.Concat(Enumerable.Repeat(token, repetitions));
        var clock = Stopwatch.StartNew();
        RunWithWatchdog(markdown);
        return clock.Elapsed;
    }

    private static void Check(string markdown)
    {
        MarkdownAnalysis analysis;
        try
        {
            analysis = RunWithWatchdog(markdown);
        }
        catch (Exception e) when (e is not TimeoutException)
        {
            Save(markdown, e.GetType().Name);
            throw new InvalidOperationException($"Analyze threw on {Show(markdown)}", e);
        }

        foreach (var reference in analysis.References)
        {
            var coherent = reference.Start >= 0
                && reference.Start < reference.End
                && reference.End <= markdown.Length
                && markdown.AsSpan(reference.Start, reference.End - reference.Start).Contains("[[", StringComparison.Ordinal);
            if (!coherent)
            {
                Save(markdown, "bad-offsets");
            }

            coherent.ShouldBeTrue($"reference at {reference.Start}..{reference.End} in {Show(markdown)}");
        }
    }

    /// <summary>The analysis on its own task: an input that hangs fails the test instead of freezing the run.</summary>
    private static MarkdownAnalysis RunWithWatchdog(string markdown)
    {
        var task = Task.Run(() => ErableMarkdown.Analyze(markdown));
        if (!task.Wait(PerInputLimit))
        {
            Save(markdown, "timeout");
            throw new TimeoutException($"Analyze did not return within {PerInputLimit.TotalSeconds:F0} s on {Show(markdown)}");
        }

        return task.Result;
    }

    private static string RandomInput(Random random)
    {
        var length = random.Next(1, 60);
        var builder = new StringBuilder();
        for (var i = 0; i < length; i++)
        {
            builder.Append(Tokens[random.Next(Tokens.Length)]);
        }

        return builder.ToString();
    }

    private static TimeSpan Budget() =>
        double.TryParse(Environment.GetEnvironmentVariable(SecondsVariable), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
            ? TimeSpan.FromSeconds(seconds)
            : TimeSpan.FromSeconds(1);

    /// <summary>Fixed by default (a failure in the regular suite must replay); the weekly run draws a new one each time.</summary>
    private static int Seed() =>
        int.TryParse(Environment.GetEnvironmentVariable("ERABLE_FUZZ_SEED"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
            ? seed
            : 42;

    private static string Show(string text) => JsonSerializer.Serialize(text.Length > 200 ? text[..200] + "…" : text);

    private static void Save(string markdown, string kind)
    {
        try
        {
            var folder = Environment.GetEnvironmentVariable("ERABLE_FUZZ_OUTPUT") is { Length: > 0 } custom
                ? custom
                : Path.Combine(AppContext.BaseDirectory, "fuzz");
            Directory.CreateDirectory(folder);
            var name = $"{kind}-{Math.Abs(markdown.GetHashCode(StringComparison.Ordinal)):X8}.json";
            File.WriteAllText(Path.Combine(folder, name), JsonSerializer.Serialize(new { name = kind, md = markdown, refs = Array.Empty<object>() }));
        }
        catch (IOException)
        {
            // The assertion message carries the input anyway.
        }
    }
}
