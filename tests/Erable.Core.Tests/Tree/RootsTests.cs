using Erable.Core.Elements;

namespace Erable.Core.Tests.Tree;

/// <summary>The default labels of the content follow the language of the project (decision Q1).</summary>
public sealed class RootsTests
{
    public static TheoryData<string> Languages => [.. ContentLanguages.All];

    [Theory]
    [MemberData(nameof(Languages))]
    public void Label_EveryContentLanguage_HasADistinctNonEmptyLabelForEachRoot(string language)
    {
        var labels = Roots.All.Select(root => Roots.Label(root, language)).ToList();

        labels.ShouldAllBe(label => label.Trim().Length > 0);
        labels.Distinct(StringComparer.Ordinal).Count().ShouldBe(Roots.All.Count);
    }

    [Fact]
    public void Label_GermanAndSpanish_AreNotTheEnglishOnes()
    {
        foreach (var root in Roots.All.Where(r => r != Roots.Pages))
        {
            Roots.Label(root, "de").ShouldNotBe(Roots.Label(root, "en"));
            Roots.Label(root, "es").ShouldNotBe(Roots.Label(root, "en"));
        }
    }

    [Fact]
    public void Label_RegionalTagOrUnknownLanguage_FallsBackToTheBaseLanguageOrEnglish()
    {
        Roots.Label(Roots.Categories, "fr-CA").ShouldBe("Catégories");
        Roots.Label(Roots.Categories, "ja").ShouldBe("Categories");
    }
}
