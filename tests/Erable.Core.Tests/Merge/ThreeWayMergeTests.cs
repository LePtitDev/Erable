using Erable.Core.Merge;

namespace Erable.Core.Tests.Merge;

/// <summary>Examples of the three-way merge (M4-05, F14.R5); the invariants are in <see cref="ThreeWayMergePropertyTests"/>.</summary>
public sealed class ThreeWayMergeTests
{
    private const string Page = """
        Ilsa tient le comptoir de Hautbois.

        ## Présentation

        Méfiante envers les soldats mais loyale envers ceux qui l'ont aidée.

        ## Histoire

        Née à Hautbois, Ilsa rejoint l'Ordre à seize ans.

        ## Rôle dans le jeu

        - Marchande de cartes.
        """;

    [Fact]
    public void Merge_EditsOfDifferentSections_AreBothKeptWithoutConflict()
    {
        var mine = Page.Replace("loyale envers ceux", "fidèle envers ceux", StringComparison.Ordinal);
        var theirs = Page.Replace("à seize ans", "à quinze ans", StringComparison.Ordinal);

        var merge = ThreeWayMerge.Merge(Page, mine, theirs);

        merge.HasConflicts.ShouldBeFalse();
        merge.Text.ShouldBe(Page
            .Replace("loyale envers ceux", "fidèle envers ceux", StringComparison.Ordinal)
            .Replace("à seize ans", "à quinze ans", StringComparison.Ordinal));
    }

    [Fact]
    public void Merge_SameSentenceChangedDifferently_IsAConflictWithTheThreeVersions()
    {
        var mine = Page.Replace("Née à Hautbois, Ilsa rejoint l'Ordre à seize ans.", "Ilsa est née à Hautbois.", StringComparison.Ordinal);
        var theirs = Page.Replace("Née à Hautbois, Ilsa rejoint l'Ordre à seize ans.", "Ilsa rejoint l'Ordre très jeune.", StringComparison.Ordinal);

        var merge = ThreeWayMerge.Merge(Page, mine, theirs);

        merge.HasConflicts.ShouldBeTrue();
        var conflict = merge.Blocks.OfType<ConflictBlock>().Single();
        conflict.Base.ShouldBe("Née à Hautbois, Ilsa rejoint l'Ordre à seize ans.\n");
        conflict.Mine.ShouldBe("Ilsa est née à Hautbois.\n");
        conflict.Theirs.ShouldBe("Ilsa rejoint l'Ordre très jeune.\n");
        // Everything around the conflict is stable, and choosing a side gives a whole page.
        merge.Resolve(c => c.Mine).ShouldBe(mine);
        merge.Resolve(c => c.Theirs).ShouldBe(theirs);
    }

    [Fact]
    public void Merge_DifferentWordsOfTheSameLine_AreMergedWordByWord()
    {
        var baseText = "Le col est fermé en hiver, la route reste dangereuse.\n";
        var mine = "Le col est fermé en hiver, la route reste très dangereuse.\n";
        var theirs = "Le passage est fermé en hiver, la route reste dangereuse.\n";

        var merge = ThreeWayMerge.Merge(baseText, mine, theirs);

        merge.HasConflicts.ShouldBeFalse();
        merge.Text.ShouldBe("Le passage est fermé en hiver, la route reste très dangereuse.\n");
    }

    [Fact]
    public void Merge_TheSameWordChangedDifferently_IsAConflict()
    {
        var merge = ThreeWayMerge.Merge("Le col est fermé.\n", "Le col est ouvert.\n", "Le col est bloqué.\n");

        merge.HasConflicts.ShouldBeTrue();
    }

    [Fact]
    public void Merge_BothMadeTheSameChange_KeepsItOnce()
    {
        var merge = ThreeWayMerge.Merge("a\nb\nc\n", "a\nB\nc\n", "a\nB\nc\n");

        merge.HasConflicts.ShouldBeFalse();
        merge.Text.ShouldBe("a\nB\nc\n");
    }

    [Fact]
    public void Merge_BothInsertedTheSameLineAtTheSamePlace_KeepsItOnce()
    {
        var merge = ThreeWayMerge.Merge("a\nc\n", "a\nb\nc\n", "a\nb\nc\n");

        merge.Text.ShouldBe("a\nb\nc\n");
    }

    [Fact]
    public void Merge_DifferentLinesInsertedAtTheSamePlace_IsAConflict()
    {
        var merge = ThreeWayMerge.Merge("a\nc\n", "a\nX\nc\n", "a\nY\nc\n");

        merge.HasConflicts.ShouldBeTrue();
        var conflict = merge.Blocks.OfType<ConflictBlock>().Single();
        conflict.Mine.ShouldBe("X\n");
        conflict.Theirs.ShouldBe("Y\n");
    }

    [Fact]
    public void Merge_OneSideDeletesWhatTheOtherLeftAlone_Deletes()
    {
        var merge = ThreeWayMerge.Merge("a\nb\nc\nd\ne\n", "a\nb\nd\ne\n", "a\nb\nc\nd\nE\n");

        merge.Text.ShouldBe("a\nb\nd\nE\n");
    }

    [Fact]
    public void Merge_OneSideDeletesWhatTheOtherEdited_IsAConflict()
    {
        var merge = ThreeWayMerge.Merge("a\nb\nc\n", "a\nc\n", "a\nB\nc\n");

        merge.HasConflicts.ShouldBeTrue();
        var conflict = merge.Blocks.OfType<ConflictBlock>().Single();
        conflict.Mine.ShouldBeEmpty();
        conflict.Theirs.ShouldBe("B\n");
    }

    [Fact]
    public void Merge_AppendsToTheEndOfTheText_AreKeptWhenOnlyOneSideAppends()
    {
        var merge = ThreeWayMerge.Merge("a\nb", "a\nb\nc", "a\nb");

        merge.Text.ShouldBe("a\nb\nc");
    }

    [Fact]
    public void Merge_EmptyBase_TakesTheNonEmptySide()
    {
        ThreeWayMerge.Merge(string.Empty, "texte\n", string.Empty).Text.ShouldBe("texte\n");
        ThreeWayMerge.Merge(string.Empty, string.Empty, "texte\n").Text.ShouldBe("texte\n");
        ThreeWayMerge.Merge(string.Empty, string.Empty, string.Empty).Text.ShouldBeEmpty();
    }

    [Fact]
    public void Merge_WindowsLineEndings_AreKeptAsTheyAre()
    {
        var merge = ThreeWayMerge.Merge("a\r\nb\r\nc\r\n", "A\r\nb\r\nc\r\n", "a\r\nb\r\nC\r\n");

        merge.Text.ShouldBe("A\r\nb\r\nC\r\n");
    }

    [Fact]
    public void Text_WithConflicts_IsRefused()
    {
        var merge = ThreeWayMerge.Merge("a\n", "b\n", "c\n");

        Should.Throw<InvalidOperationException>(() => merge.Text);
    }

    [Fact]
    public void Merge_ALongPageWithDistantEdits_StaysFast()
    {
        var lines = Enumerable.Range(0, 5000).Select(i => $"Ligne {i} du chapitre.").ToArray();
        var baseText = string.Join('\n', lines) + "\n";
        var mine = string.Join('\n', lines.Select((l, i) => i == 100 ? l + " (moi)" : l)) + "\n";
        var theirs = string.Join('\n', lines.Select((l, i) => i == 4000 ? l + " (eux)" : l)) + "\n";

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var merge = ThreeWayMerge.Merge(baseText, mine, theirs);

        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(2));
        merge.HasConflicts.ShouldBeFalse();
        merge.Text.ShouldContain("(moi)");
        merge.Text.ShouldContain("(eux)");
    }
}
