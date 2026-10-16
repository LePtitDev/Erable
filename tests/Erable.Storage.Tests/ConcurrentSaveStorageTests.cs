using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Saves that start from the same revision (M4-05, F14.R5, P-05): merged inside the transaction, or refused with both versions.</summary>
public sealed class ConcurrentSaveStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;
    private readonly FakeTimeProvider _clock = new(Chroniques.Now);

    private static readonly ElementId Ilsa = Chroniques.Ilsa;
    private static readonly ElementId Pnj = Chroniques.IdOf("Catégorie PNJ");
    private static readonly ElementId Marchand = Chroniques.IdOf("Marchand");
    private static readonly ElementId Erudit = Chroniques.IdOf("Érudit");

    public ConcurrentSaveStorageTests()
    {
        _project = Chroniques.Write(_dir.Path);
        _service = new ProjectService(_project, _clock);
    }

    public void Dispose()
    {
        _project.Dispose();
        _dir.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Markdown(ElementId page = default) => _project.Repository.GetPageContent(page.IsNone ? Ilsa : page)!.Markdown;

    private long Revision(ElementId page = default) => _project.Repository.GetPageContent(page.IsNone ? Ilsa : page)!.Revision;

    private Task<Result<PageSaved>> SaveAsync(string author, string markdown, long baseRevision, PageStructureInput? structure = null, ElementId page = default) =>
        _service.SavePageContentAsync(page.IsNone ? Ilsa : page, markdown, baseRevision, author, structure, Ct);

    private static JsonElement Json(int value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task Save_StartedBeforeAnotherSaveOnAnotherSection_IsMergedAndSaved()
    {
        // E2E-14-07
        var start = Revision();
        var original = Markdown();
        var camille = original.Replace("Méfiante envers les soldats", "Méfiante envers tous les soldats", StringComparison.Ordinal);
        var malo = original.Replace("Née à Hautbois", "Née à Hautbois-sur-Brume", StringComparison.Ordinal);
        (await SaveAsync("camille", camille, start)).IsSuccess.ShouldBeTrue();

        var saved = await SaveAsync("malo", malo, start);

        saved.IsSuccess.ShouldBeTrue();
        saved.Value.Revision.ShouldBe(start + 2);
        saved.Value.Merged.ShouldNotBeNull().MergedWith.ShouldBe(["camille"]);
        var expected = camille.Replace("Née à Hautbois", "Née à Hautbois-sur-Brume", StringComparison.Ordinal);
        Markdown().ShouldBe(expected);
        saved.Value.Merged!.Markdown.ShouldBe(expected);
        var history = _service.ListRevisions(Ilsa)!;
        history[0].Author.ShouldBe("malo");
        history[1].Author.ShouldBe("camille");
    }

    [Fact]
    public async Task Save_UpToDate_IsNotAMerge()
    {
        var saved = await SaveAsync("camille", Markdown() + "\nFin.", Revision());

        saved.IsSuccess.ShouldBeTrue();
        saved.Value.Merged.ShouldBeNull();
    }

    [Fact]
    public async Task Save_OnTheSameSentence_FailsWithTheThreeVersionsAndWritesNothing()
    {
        // E2E-14-08
        var start = Revision();
        var original = Markdown();
        const string Sentence = "Née à Hautbois, Ilsa rejoint l’Ordre à seize ans comme apprentie. Elle quitte l’Ordre après l’incendie des archives, convaincue que ses supérieurs l’ont provoqué.";
        original.ShouldContain(Sentence);
        var camille = original.Replace(Sentence, "Ilsa est née à Hautbois.", StringComparison.Ordinal);
        var malo = original.Replace(Sentence, "Ilsa rejoint l’Ordre très jeune.", StringComparison.Ordinal);
        (await SaveAsync("camille", camille, start)).IsSuccess.ShouldBeTrue();
        var revisionBefore = Revision();

        var saved = await SaveAsync("malo", malo, start);

        saved.IsSuccess.ShouldBeFalse();
        saved.Error.Code.ShouldBe("page.revisionConflict");
        var extensions = saved.Error.Extensions.ShouldNotBeNull();
        var blocks = ((MergeBlockDto[])extensions["blocks"]!);
        var conflict = blocks.Single(b => b.Kind == "conflict");
        conflict.Base.ShouldBe(Sentence + "\n");
        conflict.Mine.ShouldBe("Ilsa rejoint l’Ordre très jeune.\n");
        conflict.Theirs.ShouldBe("Ilsa est née à Hautbois.\n");
        ((MergeCurrent)extensions["current"]!).Revision.ShouldBe(revisionBefore);
        ((IReadOnlyList<string>)extensions["mergedWith"]!).ShouldBe(["camille"]);
        // Nothing was written: camille's version is still the page, and malo's is not in the history.
        Markdown().ShouldBe(camille);
        Revision().ShouldBe(revisionBefore);
        _service.ListRevisions(Ilsa)!.Select(r => r.Author).ShouldNotContain("malo");
    }

    [Fact]
    public async Task Save_AfterResolvingAConflict_WorksAgainstTheCurrentRevision()
    {
        // E2E-14-08: malo chooses his version; the version of camille stays in the history.
        var start = Revision();
        var original = Markdown();
        const string Sentence = "Née à Hautbois, Ilsa rejoint l’Ordre à seize ans comme apprentie. Elle quitte l’Ordre après l’incendie des archives, convaincue que ses supérieurs l’ont provoqué.";
        var camille = original.Replace(Sentence, "Ilsa est née à Hautbois.", StringComparison.Ordinal);
        var malo = original.Replace(Sentence, "Ilsa rejoint l’Ordre très jeune.", StringComparison.Ordinal);
        (await SaveAsync("camille", camille, start)).IsSuccess.ShouldBeTrue();
        (await SaveAsync("malo", malo, start)).IsSuccess.ShouldBeFalse();

        var resolved = await SaveAsync("malo", malo, Revision());

        resolved.IsSuccess.ShouldBeTrue();
        resolved.Value.Merged.ShouldBeNull();
        Markdown().ShouldBe(malo);
        _service.GetRevision(Ilsa, start + 1)!.Snapshot.Markdown.ShouldBe(camille);
    }

    [Fact]
    public async Task Save_WithAnUnknownBaseRevision_IsPlainlyRefused()
    {
        var saved = await SaveAsync("malo", "texte", Revision() + 5);

        saved.Error.Code.ShouldBe("page.revisionConflict");
        saved.Error.Extensions.ShouldBeNull();
    }

    [Fact]
    public async Task Save_CategoriesChangedByBothSides_AreCombined()
    {
        var start = Revision();
        var original = Markdown();
        // Ilsa is PNJ + Marchand. Camille adds Érudit; malo, who did not see it, removes Marchand.
        (await SaveAsync("camille", original, start, new PageStructureInput([Pnj, Marchand, Erudit], null))).IsSuccess.ShouldBeTrue();

        var saved = await SaveAsync("malo", original + "\nNote de malo.", start, new PageStructureInput([Pnj], null));

        saved.IsSuccess.ShouldBeTrue();
        saved.Value.Merged!.Categories.ShouldNotBeNull().ShouldBe([Pnj, Erudit], ignoreOrder: true);
        _service.GetRevision(Ilsa, Revision())!.Snapshot.Categories!.ShouldBe([Pnj, Erudit], ignoreOrder: true);
    }

    [Fact]
    public async Task Save_ValuesChangedByDifferentSides_AreCombinedFieldByField()
    {
        var start = Revision();
        var original = Markdown();
        var niveau = Chroniques.Niveau;
        var age = Chroniques.IdOf("Âge");
        (await SaveAsync("camille", original, start, new PageStructureInput(null, [(niveau, Json(40))]))).IsSuccess.ShouldBeTrue();

        var saved = await SaveAsync("malo", original, start, new PageStructureInput(null, [(niveau, Json(72)), (age, Json(30))]));

        saved.IsSuccess.ShouldBeTrue();
        var values = saved.Value.Merged!.Values!;
        // Malo did not change the level (72 as at the start): camille's 40 stays; the age is his. Both sides dropped the role and the region: they stay dropped.
        values[niveau.ToString()]!.ToJsonString().ShouldBe("40");
        values[age.ToString()]!.ToJsonString().ShouldBe("30");
    }

    [Fact]
    public async Task Save_SameValueChangedDifferently_FailsWithTheValueConflict()
    {
        var start = Revision();
        var original = Markdown();
        var niveau = Chroniques.Niveau;
        (await SaveAsync("camille", original, start, new PageStructureInput(null, [(niveau, Json(40))]))).IsSuccess.ShouldBeTrue();
        var revisionBefore = Revision();

        var saved = await SaveAsync("malo", original, start, new PageStructureInput(null, [(niveau, Json(30))]));

        saved.IsSuccess.ShouldBeFalse();
        var conflicts = (IReadOnlyList<Erable.Core.Merge.ValueConflict>)saved.Error.Extensions!["valueConflicts"]!;
        var conflict = conflicts.ShouldHaveSingleItem();
        conflict.PropertyId.ShouldBe(niveau.ToString());
        conflict.Mine!.ToJsonString().ShouldBe("30");
        conflict.Theirs!.ToJsonString().ShouldBe("40");
        Revision().ShouldBe(revisionBefore);
    }

    [Fact]
    public async Task Save_WithoutStructure_LeavesTheOthersCategoriesAlone()
    {
        var start = Revision();
        var original = Markdown();
        (await SaveAsync("camille", original, start, new PageStructureInput([Pnj, Erudit], null))).IsSuccess.ShouldBeTrue();

        var saved = await SaveAsync("malo", original + "\nNote.", start);

        saved.IsSuccess.ShouldBeTrue();
        saved.Value.Merged!.Categories.ShouldBeNull();
        _service.GetRevision(Ilsa, Revision())!.Snapshot.Categories!.ShouldBe([Pnj, Erudit], ignoreOrder: true);
    }

    [Fact]
    public async Task ManyWritersFromTheSameRevision_LoseNothing()
    {
        // « N écritures » on one page, all started from the same revision, each in its own place: every line survives.
        var start = Revision();
        var original = Markdown();
        var lines = original.Split('\n');
        const int Writers = 8;
        // Edit lines far apart (the page has about 25 lines): none touches another.
        var places = Enumerable.Range(0, Writers).Select(i => i * 3).Where(i => i < lines.Length).ToArray();
        places.Length.ShouldBeGreaterThan(3);

        await Parallel.ForEachAsync(places.Select((line, i) => (line, i)), Ct, async (job, token) =>
        {
            var edited = (string[])lines.Clone();
            edited[job.line] += $" [rédacteur {job.i}]";
            var saved = await _service.SavePageContentAsync(Ilsa, string.Join('\n', edited), start, $"user{job.i}", null, token);
            // Writers that lose the race on the same lines would conflict; here every edit is independent.
            saved.IsSuccess.ShouldBeTrue(saved.IsSuccess ? string.Empty : saved.Error.Code);
        });

        var final = Markdown();
        for (var i = 0; i < places.Length; i++)
        {
            final.ShouldContain($"[rédacteur {i}]");
        }

        Revision().ShouldBe(start + places.Length);
    }
}
