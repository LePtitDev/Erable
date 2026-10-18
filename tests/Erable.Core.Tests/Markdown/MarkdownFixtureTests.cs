using Erable.Core.Markdown;
using Erable.Testing;

namespace Erable.Core.Tests.Markdown;

/// <summary>spec/fixtures/markdown, shared with src/web/src/shared/markdown/markdown.fixtures.test.ts.</summary>
public sealed class MarkdownFixtureTests
{
    public static TheoryData<FixtureCase> ReferenceCases => Only("refs.json", "edge-cases.json");

    public static TheoryData<FixtureCase> PlainTextCases => Only("plain-text.json");

    public static TheoryData<FixtureCase> HeadingCases => Only("headings.json");

    public static TheoryData<FixtureCase> SlugCases => Only("slugs.json");

    [Theory]
    [MemberData(nameof(ReferenceCases))]
    public void Analyze_SharedCases_FindsExpectedReferences(FixtureCase fixture)
    {
        var c = fixture.As<RefCase>();

        var actual = ErableMarkdown.Analyze(c.Md).References.Select(r => new ExpectedRef(
            r.Id.ToString(), r.Anchor, r.Label, r.IsEmbed,
            r.Attributes is null ? null : new ExpectedAttrs(r.Attributes.Width, r.Attributes.Align), r.Start, r.End));

        if (c.KnownFailure == "csharp")
        {
            // A known gap kept visible (audit T7): this turns red once fixed, so that the marker gets removed.
            actual.ShouldNotBe(c.Refs, $"'{c.Name}' now passes: remove its knownFailure marker.");
            return;
        }

        actual.ShouldBe(c.Refs);
    }

    [Theory]
    [MemberData(nameof(PlainTextCases))]
    public void Analyze_SharedCases_ExtractsPlainText(FixtureCase fixture)
    {
        var c = fixture.As<PlainCase>();

        ErableMarkdown.Analyze(c.Md).PlainText.ShouldBe(c.Plain);
    }

    [Theory]
    [MemberData(nameof(HeadingCases))]
    public void Analyze_SharedCases_FindsHeadingsAndAnchors(FixtureCase fixture)
    {
        var c = fixture.As<HeadingCase>();

        ErableMarkdown.Analyze(c.Md).Headings.Select(h => new ExpectedHeading(h.Level, h.Text, h.Anchor)).ShouldBe(c.Headings);
    }

    [Theory]
    [MemberData(nameof(SlugCases))]
    public void Slugify_SharedCases_MatchesSpecification(FixtureCase fixture)
    {
        var c = fixture.As<SlugCase>();

        Slugs.Slugify(c.Text).ShouldBe(c.Slug);
    }

    private static TheoryData<FixtureCase> Only(params string[] files)
    {
        var data = new TheoryData<FixtureCase>();
        foreach (var row in SharedFixtures.Load("markdown"))
        {
            var fixture = row.Data;
            if (files.Contains(fixture.File))
            {
                data.Add(fixture);
            }
        }

        return data;
    }

    public sealed record ExpectedAttrs(int? Width, string? Align);

    public sealed record ExpectedRef(string Id, string? Anchor, string? Label, bool Embed, ExpectedAttrs? Attrs, int Start, int End);

    /// <param name="KnownFailure">« csharp »: a documented gap of the C# side, which must still fail.</param>
    public sealed record RefCase(string Name, string Md, IReadOnlyList<ExpectedRef> Refs, string? KnownFailure = null);

    public sealed record PlainCase(string Name, string Md, string Plain);

    public sealed record ExpectedHeading(int Level, string Text, string Anchor);

    public sealed record HeadingCase(string Name, string Md, IReadOnlyList<ExpectedHeading> Headings);

    public sealed record SlugCase(string Name, string Text, string Slug);
}
