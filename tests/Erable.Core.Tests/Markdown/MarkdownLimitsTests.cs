using Erable.Core.Markdown;
using Erable.Testing;

namespace Erable.Core.Tests.Markdown;

/// <summary>spec/fixtures/limits, shared with src/web/src/shared/markdown/limits.fixtures.test.ts (M5-04).</summary>
public sealed class MarkdownLimitsTests
{
    public static TheoryData<FixtureCase> Cases => SharedFixtures.Load("limits");

    public sealed record LimitCase(string Name, string Md, string Unit, int Count, string? Rule, int Line);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Check_SharedCases_ReportsTheFirstBrokenLimit(FixtureCase fixture)
    {
        var c = fixture.As<LimitCase>();
        var markdown = c.Md + string.Concat(Enumerable.Repeat(c.Unit, c.Count));

        var violation = MarkdownLimits.Check(markdown);

        violation?.Rule.ToString().ShouldBe(c.Rule is null ? null : char.ToUpperInvariant(c.Rule[0]) + c.Rule[1..]);
        (violation is null).ShouldBe(c.Rule is null);
        (violation?.Line ?? 0).ShouldBe(c.Line);
    }

    /// <summary>The point of the limits: whatever they let through, the analysis stays fast and does not crash.</summary>
    [Theory]
    [InlineData("[a](")]
    [InlineData("> ")]
    [InlineData("[")]
    [InlineData("1. ")]
    [InlineData("]")]
    public void Analyze_LargestAcceptedRepetition_StaysUnderTheBudget(string token)
    {
        var count = MarkdownLimits.MaxLineLength / token.Length;
        var markdown = string.Concat(Enumerable.Repeat(token, count));
        var accepted = MarkdownLimits.Check(markdown) is null;
        if (!accepted)
        {
            // « > » and « 1. » are refused by the nesting rule long before the line is full: take what is accepted.
            count = MarkdownLimits.MaxNesting;
            markdown = string.Concat(Enumerable.Repeat(token, count));
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        ErableMarkdown.Analyze(markdown);

        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Check_CrLfAndLfPages_AgreeOnTheLine()
    {
        var lf = MarkdownLimits.Check("ok\n" + new string('a', MarkdownLimits.MaxLineLength + 1));
        var crlf = MarkdownLimits.Check("ok\r\n" + new string('a', MarkdownLimits.MaxLineLength + 1));

        lf.ShouldBe(crlf);
        lf!.Line.ShouldBe(2);
    }
}
