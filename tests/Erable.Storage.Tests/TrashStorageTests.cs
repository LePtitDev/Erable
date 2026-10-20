using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Deleting, the trash, restoring and emptying, on the reference project (F12).</summary>
public sealed class TrashStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;
    private readonly FakeTimeProvider _clock = new(Chroniques.Now);

    private static readonly ElementId Marchand = Chroniques.IdOf("Marchand");
    private static readonly ElementId Dialogues = Chroniques.IdOf("Dialogues");
    private static readonly ElementId Inventaire = Chroniques.IdOf("Inventaire");
    private static readonly ElementId Lieux = Chroniques.IdOf("Lieux");
    private static readonly ElementId SituéÀ = Chroniques.IdOf("Situé à");
    private static readonly ElementId Carte = Chroniques.IdOf("carte_hautbois.webp");

    public TrashStorageTests()
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

    private async Task DeleteAsync(ElementId id)
    {
        _clock.Advance(TimeSpan.FromMinutes(1));
        (await _service.DeleteElementAsync(id, "camille", Ct)).IsSuccess.ShouldBeTrue();
    }

    private bool IsLive(ElementId id) => _service.Resolve([id]).Single().State == ResolvedState.Existing;

    /// <summary>The derived tables and values, as text: two equal dumps mean the project is the same (dates apart).</summary>
    private string Dump()
    {
        using var connection = _project.Database.OpenConnection();
        string Rows(string sql) => string.Join('\n', connection.Query(sql).Select(r => string.Join('|', ((IDictionary<string, object>)r).Values)));
        return string.Join("\n--\n",
            Rows("SELECT id, kind, parent_id, name, sort_order, deleted_at FROM elements ORDER BY id"),
            Rows("SELECT * FROM resolved_values ORDER BY page_id, property_id, idx"),
            Rows("SELECT * FROM validation_issues ORDER BY owner_id, property_id, rule_index, item_index"),
            Rows("SELECT * FROM property_values ORDER BY owner_id, property_id, idx"),
            Rows("SELECT * FROM page_categories ORDER BY page_id, sort_order"),
            Rows("SELECT * FROM links ORDER BY id"));
    }

    // ─── Impact ─────────────────────────────────────────────────────────────

    [Fact]
    public void GetDeleteImpact_PageMentionedByAnother_ListsIt()
    {
        // E2E-12-01
        var impact = _service.GetDeleteImpact(Chroniques.Orsen)!;

        impact.ReferencedBy.Select(i => i.Name).ShouldBe(["Ilsa Varenne"]);
        impact.SubPages.ShouldBe(0);
    }

    [Fact]
    public void GetDeleteImpact_PageWithSubPages_CountsThem()
    {
        // E2E-12-03
        var impact = _service.GetDeleteImpact(Chroniques.Ilsa)!;

        impact.SubPages.ShouldBe(2);
        impact.Descendants.ShouldBe(3); // two sub-pages and the portrait
    }

    [Fact]
    public void GetDeleteImpact_Attachment_CountsOnlyTheOtherPagesUsingIt()
    {
        // E2E-06-06: Ilsa shows the map of Hautbois.
        var impact = _service.GetDeleteImpact(Carte)!;

        impact.ReferencedBy.Select(i => i.Name).ShouldBe(["Ilsa Varenne"]);
    }

    [Fact]
    public void GetDeleteImpact_LinkType_CountsItsLinks()
    {
        // E2E-09-05
        _service.GetDeleteImpact(SituéÀ)!.Links.ShouldBe(1);
    }

    [Fact]
    public void GetDeleteImpact_Category_CountsThePagesThatLoseIt()
    {
        _service.GetDeleteImpact(Marchand)!.Pages.ShouldBe(1);
    }

    [Fact]
    public void GetDeleteImpact_Property_CountsTheOwnersOfValues()
    {
        // Niveau: Ilsa, Orsen, Dragon and the categories giving a default.
        _service.GetDeleteImpact(Chroniques.Niveau)!.Values.ShouldBeGreaterThan(3);
    }

    [Fact]
    public void GetDeleteImpact_Unknown_IsNull()
    {
        _service.GetDeleteImpact(ElementId.Parse("DEADBEEF00000001")).ShouldBeNull();
    }

    // ─── Delete ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_Page_LeavesTheTreeAndReferencesBreak()
    {
        await DeleteAsync(Chroniques.Orsen);

        IsLive(Chroniques.Orsen).ShouldBeFalse();
        _service.Resolve([Chroniques.Orsen]).Single().State.ShouldBe(ResolvedState.Deleted);
        _service.LoadTree().Contains(Chroniques.Orsen).ShouldBeFalse();
        var entry = _service.ListTrash().Single();
        (entry.Id, entry.Name, entry.Kind, entry.DeletedBy).ShouldBe((Chroniques.Orsen, "Capitaine Orsen", ElementKind.Page, "camille"));
        entry.Path.Skip(1).Select(n => n.Name).ShouldBe(["Personnages", "PNJ"]);
        entry.ParentAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Page_RemovesItsDerivedValuesAndLinks()
    {
        await DeleteAsync(Chroniques.Orsen);

        using var connection = _project.Database.OpenConnection();
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM resolved_values WHERE page_id = @Id", new { Id = Chroniques.Orsen.ToInt64() }).ShouldBe(0);
        _service.GetPageLinks(Chroniques.Ilsa)!.Outgoing.ShouldNotContain(l => l.OtherId == Chroniques.Orsen);
    }

    [Fact]
    public async Task Delete_PageWithSubPages_TakesThemAlong()
    {
        // E2E-12-03
        await DeleteAsync(Chroniques.Ilsa);

        IsLive(Dialogues).ShouldBeFalse();
        IsLive(Inventaire).ShouldBeFalse();
        var entry = _service.ListTrash().Single();
        entry.Id.ShouldBe(Chroniques.Ilsa);
        entry.Descendants.ShouldBe(3);
    }

    [Fact]
    public async Task Delete_Root_IsRefused()
    {
        var result = await _service.DeleteElementAsync(Roots.Pages.Id, "camille", Ct);

        result.Error.Code.ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public async Task Delete_Unknown_IsRefused()
    {
        var result = await _service.DeleteElementAsync(ElementId.Parse("DEADBEEF00000001"), "camille", Ct);

        result.Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
    }

    [Fact]
    public async Task Delete_Twice_IsRefused()
    {
        await DeleteAsync(Chroniques.Orsen);

        (await _service.DeleteElementAsync(Chroniques.Orsen, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
    }

    [Fact]
    public async Task Delete_Category_LeavesThePagesWithoutIt()
    {
        // E2E-12-04
        await DeleteAsync(Marchand);

        IsLive(Chroniques.Ilsa).ShouldBeTrue();
        _service.LoadStructure([Chroniques.Ilsa]).CategoriesOf(Chroniques.Ilsa).Select(c => c).ShouldBe([Chroniques.Pnj]);
    }

    [Fact]
    public async Task Delete_Property_HidesItsValuesEverywhere()
    {
        await DeleteAsync(Chroniques.Niveau);

        var state = _service.LoadStructure(null);
        state.ResolvePage(Chroniques.Ilsa).ShouldNotContain(p => p.Definition.Id == Chroniques.Niveau);
        using var connection = _project.Database.OpenConnection();
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM resolved_values WHERE property_id = @Id", new { Id = Chroniques.Niveau.ToInt64() }).ShouldBe(0);
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM validation_issues WHERE property_id = @Id", new { Id = Chroniques.Niveau.ToInt64() }).ShouldBe(0);
    }

    [Fact]
    public async Task Delete_LinkType_HidesItsLinks()
    {
        await DeleteAsync(SituéÀ);

        _service.GetPageLinks(Chroniques.Ilsa)!.Outgoing.ShouldNotContain(l => l.TypeName == "Situé à");
        _service.ListLinkTypes().ShouldNotContain(t => t.Id == SituéÀ);
    }

    // ─── Restore ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Restore_Page_ReturnsToItsPlaceWithTheSameId()
    {
        // E2E-12-02
        await DeleteAsync(Chroniques.Orsen);

        var restored = await _service.RestoreAsync(Chroniques.Orsen, "camille", Ct);

        restored.IsSuccess.ShouldBeTrue();
        restored.Value.Relocated.ShouldBeFalse();
        IsLive(Chroniques.Orsen).ShouldBeTrue();
        _service.GetElement(Chroniques.Orsen)!.Path.Skip(1).Select(n => n.Name).ShouldBe(["Personnages", "PNJ"]);
        _service.ListTrash().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Capitaine Orsen")]
    [InlineData("Ilsa Varenne")]
    [InlineData("Marchand")]
    [InlineData("Niveau")]
    [InlineData("Situé à")]
    [InlineData("Lieux")]
    [InlineData("carte_hautbois.webp")]
    public async Task DeleteThenRestore_LeavesTheProjectIdentical(string name)
    {
        var before = Dump();

        await DeleteAsync(Chroniques.IdOf(name));
        Dump().ShouldNotBe(before);
        (await _service.RestoreAsync(Chroniques.IdOf(name), "camille", Ct)).IsSuccess.ShouldBeTrue();

        Dump().ShouldBe(before);
    }

    [Fact]
    public async Task Restore_PageWithSubPages_BringsThemBack()
    {
        // E2E-12-03
        await DeleteAsync(Chroniques.Ilsa);

        var restored = await _service.RestoreAsync(Chroniques.Ilsa, "camille", Ct);

        restored.Value.Restored.ShouldBe(4);
        IsLive(Dialogues).ShouldBeTrue();
        IsLive(Inventaire).ShouldBeTrue();
    }

    [Fact]
    public async Task Restore_ParentDeletedAfterwards_DoesNotBringBackWhatWasDeletedBefore()
    {
        await DeleteAsync(Dialogues);
        await DeleteAsync(Chroniques.Ilsa);

        await _service.RestoreAsync(Chroniques.Ilsa, "camille", Ct);

        IsLive(Inventaire).ShouldBeTrue();
        IsLive(Dialogues).ShouldBeFalse();
        _service.ListTrash().Select(e => e.Id).ShouldBe([Dialogues]);
    }

    [Fact]
    public async Task Restore_Category_GivesItBackToThePages()
    {
        await DeleteAsync(Marchand);

        await _service.RestoreAsync(Marchand, "camille", Ct);

        _service.LoadStructure([Chroniques.Ilsa]).CategoriesOf(Chroniques.Ilsa).ShouldBe([Chroniques.Pnj, Marchand]);
    }

    [Fact]
    public async Task Restore_ParentInTheTrash_GoesToTheRootOfItsKind()
    {
        await DeleteAsync(Chroniques.Hautbois);
        await DeleteAsync(Lieux);

        var restored = await _service.RestoreAsync(Chroniques.Hautbois, "camille", Ct);

        restored.Value.Relocated.ShouldBeTrue();
        restored.Value.ParentId.ShouldBe(Roots.Pages.Id);
        IsLive(Chroniques.Hautbois).ShouldBeTrue();
    }

    [Fact]
    public async Task Restore_ParentEmptiedFromTheTrash_GoesToTheRootWithTheMessage()
    {
        // E2E-12-05
        await DeleteAsync(Chroniques.Hautbois);
        await DeleteAsync(Lieux);
        (await _service.PurgeAsync([Lieux], Ct)).Value.ShouldBeGreaterThan(0);

        var entry = _service.ListTrash().Single();
        entry.Id.ShouldBe(Chroniques.Hautbois);
        entry.ParentAvailable.ShouldBeFalse();
        var restored = await _service.RestoreAsync(Chroniques.Hautbois, "camille", Ct);

        restored.Value.Relocated.ShouldBeTrue();
        restored.Value.ParentId.ShouldBe(Roots.Pages.Id);
    }

    [Fact]
    public async Task Restore_AttachmentOfADeletedPage_IsRefused()
    {
        await DeleteAsync(Carte);
        await DeleteAsync(Chroniques.Hautbois);

        var restored = await _service.RestoreAsync(Carte, "camille", Ct);

        restored.Error.Code.ShouldBe(ErrorCodes.TrashParentDeleted);
    }

    [Fact]
    public async Task Restore_NotInTheTrash_IsRefused()
    {
        (await _service.RestoreAsync(Chroniques.Orsen, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.TrashEntryNotFound);
    }

    [Fact]
    public async Task Restore_ElementInsideAnEntry_IsRefused()
    {
        await DeleteAsync(Chroniques.Ilsa);

        (await _service.RestoreAsync(Dialogues, "camille", Ct)).Error.Code.ShouldBe(ErrorCodes.TrashEntryNotFound);
    }

    // ─── Empty ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task Purge_All_RemovesEverythingAndTheBlobsNothingReferences()
    {
        // E2E-12-06
        var portrait = _project.Repository.GetAttachment(Chroniques.IdOf("portrait_ilsa.png"))!;
        await DeleteAsync(Chroniques.Ilsa);
        await DeleteAsync(Marchand);
        _project.Blobs.OpenRead(portrait.Sha256).ShouldNotBeNull().Dispose();

        var purged = await _service.PurgeAsync(null, Ct);

        purged.Value.ShouldBe(5);
        _service.ListTrash().ShouldBeEmpty();
        _service.Resolve([Chroniques.Ilsa]).Single().State.ShouldBe(ResolvedState.Unknown);
        _project.Blobs.OpenRead(portrait.Sha256).ShouldBeNull();
        using var connection = _project.Database.OpenConnection();
        connection.Query("PRAGMA foreign_key_check;").ShouldBeEmpty();
    }

    [Fact]
    public async Task Purge_Entry_KeepsTheOthers()
    {
        await DeleteAsync(Chroniques.Orsen);
        await DeleteAsync(Marchand);

        await _service.PurgeAsync([Marchand], Ct);

        _service.ListTrash().Select(e => e.Id).ShouldBe([Chroniques.Orsen]);
    }

    [Fact]
    public async Task Purge_NotAnEntry_IsRefused()
    {
        (await _service.PurgeAsync([Chroniques.Orsen], Ct)).Error.Code.ShouldBe(ErrorCodes.TrashEntryNotFound);
    }

    [Fact]
    public async Task Purge_Everything_LeavesAProjectTheDoctorAccepts()
    {
        await DeleteAsync(Chroniques.Ilsa);
        await DeleteAsync(Chroniques.Niveau);
        await DeleteAsync(SituéÀ);
        await DeleteAsync(Marchand);
        await _service.PurgeAsync(null, Ct);

        var report = ProjectDoctor.Diagnose(_project, fix: false);

        report.Findings.Where(f => f.Severity != DoctorSeverity.Info && f.Code != DoctorCodes.IndexOutOfSync).ShouldBeEmpty();
    }
}
