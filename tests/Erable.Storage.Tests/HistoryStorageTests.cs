using System.Text.Json;
using Erable.Core;
using Erable.Core.History;
using Erable.Core.Search;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Revision history: list, comparison, restoration (F13).</summary>
public sealed class HistoryStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;
    private readonly FakeTimeProvider _clock = new(Chroniques.Now);

    private static readonly ElementId Hautbois = Chroniques.Hautbois;
    private static readonly ElementId Marchand = Chroniques.IdOf("Marchand");
    private static readonly ElementId Region = Chroniques.IdOf("Catégorie Région");

    public HistoryStorageTests()
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

    private async Task<long> SaveAsync(ElementId page, string markdown, string author = "camille", PageStructureInput? structure = null)
    {
        _clock.Advance(TimeSpan.FromMinutes(5));
        var current = _project.Repository.GetPageContent(page)!.Revision;
        return (await _service.SavePageContentAsync(page, markdown, current, author, structure, Ct)).Value.Revision;
    }

    private string Markdown(ElementId page) => _project.Repository.GetPageContent(page)!.Markdown;

    private static JsonElement Json(int value) => JsonSerializer.SerializeToElement(value);

    [Fact]
    public async Task ListRevisions_PageSavedThreeTimes_HasFourNewestFirst()
    {
        // E2E-13-01
        await SaveAsync(Hautbois, "un");
        await SaveAsync(Hautbois, "un\ndeux", "malo");
        await SaveAsync(Hautbois, "un\ndeux\ntrois");

        var history = _service.ListRevisions(Hautbois)!;

        history.Select(r => r.Revision).ShouldBe([4, 3, 2, 1]);
        history.Select(r => r.Author).ShouldBe(["camille", "malo", "camille", "malo"]);
        history[0].At.ShouldBeGreaterThan(history[1].At);
        history[3].Changes.ShouldBe([RevisionChange.Created]);
        history[0].Changes.ShouldBe([RevisionChange.Text]);
        history.ShouldAllBe(r => r.RestoredFrom == null);
    }

    [Fact]
    public async Task ListRevisions_ChangesOfNameCategoriesAndValues_AreTold()
    {
        await _service.UpdateElementAsync(Hautbois, "Brumeval", null, null, "camille", Ct);
        await SaveAsync(Hautbois, Markdown(Hautbois), structure: new PageStructureInput([Region, Marchand], [(Chroniques.Niveau, Json(12))]));

        var history = _service.ListRevisions(Hautbois)!;

        history[1].Changes.ShouldBe([RevisionChange.Name]);
        history[0].Changes.ShouldBe([RevisionChange.Categories, RevisionChange.Properties]);
    }

    [Fact]
    public void ListRevisions_UnknownElement_IsNull()
    {
        _service.ListRevisions(ElementId.Parse("DEADBEEF00000001")).ShouldBeNull();
    }

    [Fact]
    public async Task DiffRevisions_TextCategoriesAndProperties_AreCompared()
    {
        // E2E-13-02
        var first = Markdown(Hautbois);
        await SaveAsync(Hautbois, first.Replace("Bourg marchand", "Bourg fortifié"), structure: new PageStructureInput([Region], [(Chroniques.Niveau, Json(12))]));
        await SaveAsync(Hautbois, first + "\nUn puits.", structure: new PageStructureInput([Region, Marchand], [(Chroniques.Niveau, Json(15))]));

        var diff = _service.DiffRevisions(Hautbois, 3, 1).Value;

        diff.From.Entry.Revision.ShouldBe(1);
        diff.To.Entry.Revision.ShouldBe(3);
        diff.Text.Where(r => r.Kind == DiffRowKind.Added).Select(r => r.New!.Text).ShouldContain("Un puits.");
        diff.CategoriesAdded.ShouldBe([Marchand]);
        diff.CategoriesRemoved.ShouldBeEmpty();
        var change = diff.Values.Single();
        (change.PropertyId, change.Old, change.New!.GetValue<int>()).ShouldBe((Chroniques.Niveau, null, 15));
    }

    [Fact]
    public void DiffRevisions_UnknownRevision_IsNotFound()
    {
        _service.DiffRevisions(Hautbois, 1, 9).Error.Code.ShouldBe(ErrorCodes.RevisionNotFound);
    }

    [Fact]
    public async Task RestoreRevision_CreatesANewRevisionWithTheOldContent()
    {
        // E2E-13-03
        var original = Markdown(Hautbois);
        await SaveAsync(Hautbois, "deux");
        await SaveAsync(Hautbois, "trois");
        await SaveAsync(Hautbois, "quatre");

        var restored = (await _service.RestoreRevisionAsync(Hautbois, 2, "malo", Ct)).Value;

        restored.Revision.ShouldBe(5);
        Markdown(Hautbois).ShouldBe("deux");
        var history = _service.ListRevisions(Hautbois)!;
        history.Count.ShouldBe(5);
        history[0].RestoredFrom.ShouldBe(2);
        (history[0].Author, history[0].Revision).ShouldBe(("malo", 5));
        history.Skip(1).ShouldAllBe(r => r.RestoredFrom == null);
        _service.GetRevision(Hautbois, 1)!.Snapshot.Markdown.ShouldBe(original);
        _service.GetRevision(Hautbois, 4)!.Snapshot.Markdown.ShouldBe("quatre");
    }

    [Fact]
    public async Task RestoreRevision_BringsBackCategoriesAndValuesAndRefreshesTheIndexes()
    {
        await SaveAsync(Hautbois, "zéphyr cassé", structure: new PageStructureInput([Region, Marchand], [(Chroniques.Niveau, Json(33))]));
        await SaveAsync(Hautbois, "sans rien", structure: new PageStructureInput([], []));

        (await _service.RestoreRevisionAsync(Hautbois, 2, "camille", Ct)).IsSuccess.ShouldBeTrue();

        using var connection = _project.Database.OpenConnection();
        StructureState.Load(connection, [Hautbois]).CategoriesOf(Hautbois).ShouldBe([Region, Marchand]);
        var request = new SearchRequest("rien", false, SearchFilter.None, SearchSort.Relevance, 0, 5);
        _service.Search(request).Hits.ShouldBeEmpty();
        _service.Search(request with { Text = "zephyr" }).Hits.Select(h => h.Element.Id).ShouldBe([Hautbois]);
        ProjectDoctor.Diagnose(_project, fix: false).Findings.Where(f => f.Severity != DoctorSeverity.Info).ShouldBeEmpty();
    }

    [Fact]
    public async Task RestoreRevision_DeletedCategory_IsSkippedAndCounted()
    {
        await SaveAsync(Hautbois, "x", structure: new PageStructureInput([Region, Marchand], null));
        await SaveAsync(Hautbois, "y", structure: new PageStructureInput([], null));
        await _service.DeleteElementAsync(Marchand, "camille", Ct);

        var restored = (await _service.RestoreRevisionAsync(Hautbois, 2, "camille", Ct)).Value;

        restored.SkippedCategories.ShouldBe(1);
        _service.GetRevision(Hautbois, restored.Revision)!.Snapshot.Categories.ShouldBe([Region]);
    }

    [Fact]
    public async Task RestoreRevision_UnknownRevisionNonPageOrDeletedPage_IsRefused()
    {
        (await _service.RestoreRevisionAsync(Hautbois, 42, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.RevisionNotFound);
        (await _service.RestoreRevisionAsync(Chroniques.Pnj, 1, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.RevisionNotRestorable);
        await _service.DeleteElementAsync(Hautbois, "camille", Ct);
        (await _service.RestoreRevisionAsync(Hautbois, 1, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.RevisionNotRestorable);
    }

    [Fact]
    public async Task RestoreRevision_AnyRevision_GivesContentIdenticalToIt()
    {
        foreach (var text in new[] { "premier", "premier\nsecond", "troisième ligne", string.Empty, "dernier" })
        {
            await SaveAsync(Hautbois, text);
        }

        var revisions = _service.ListRevisions(Hautbois)!.Select(r => r.Revision).ToArray();
        foreach (var revision in revisions)
        {
            var expected = _service.GetRevision(Hautbois, revision)!.Snapshot.Markdown;

            await _service.RestoreRevisionAsync(Hautbois, revision, "camille", Ct);

            Markdown(Hautbois).ShouldBe(expected);
        }

        _service.ListRevisions(Hautbois)!.Count.ShouldBe(revisions.Length * 2);
    }

    [Fact]
    public async Task Restore_DoesNotRewriteThePast()
    {
        await SaveAsync(Hautbois, "a");
        await SaveAsync(Hautbois, "b");
        static (long, string, DateTimeOffset, string) Key(RevisionEntry r) => (r.Revision, r.Author, r.At, string.Join(',', r.Changes));
        var before = _service.ListRevisions(Hautbois)!.Select(Key).ToList();

        await _service.RestoreRevisionAsync(Hautbois, 1, "camille", Ct);

        _service.ListRevisions(Hautbois)!.Skip(1).Select(Key).ShouldBe(before);
        _service.GetRevision(Hautbois, 9).ShouldBeNull();
    }
}
