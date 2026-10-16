using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Recently viewed and edited elements (M3-04, F11).</summary>
public sealed class RecentStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;
    private readonly FakeTimeProvider _clock = new(Chroniques.Now);

    private static readonly ElementId Pnj = Chroniques.Pnj;
    private static readonly ElementId Niveau = Chroniques.Niveau;

    public RecentStorageTests()
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

    private async Task ViewAsync(string user, ElementId id)
    {
        _clock.Advance(TimeSpan.FromSeconds(5));
        (await _service.RecordViewAsync(user, id, Ct)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task GetRecent_Views_AreNewestFirstWithTheirKind()
    {
        // E2E-11-01
        await ViewAsync("camille", Pnj);
        await ViewAsync("camille", Chroniques.Hautbois);
        await ViewAsync("camille", Niveau);

        var viewed = _service.GetRecent("camille", 10).Viewed;

        viewed.Select(v => v.Element.Name).ShouldBe(["Niveau", "Hautbois", "PNJ"]);
        viewed.Select(v => v.Element.Kind).ShouldBe([ElementKind.PropertyDefinition, ElementKind.Page, ElementKind.Category]);
    }

    [Fact]
    public async Task GetRecent_ViewedAgain_MovesToTheTopOnce()
    {
        await ViewAsync("camille", Pnj);
        await ViewAsync("camille", Chroniques.Hautbois);
        await ViewAsync("camille", Pnj);

        _service.GetRecent("camille", 10).Viewed.Select(v => v.Element.Name).ShouldBe(["PNJ", "Hautbois"]);
    }

    [Fact]
    public async Task GetRecent_Views_BelongToTheirUser()
    {
        await ViewAsync("camille", Chroniques.Hautbois);
        await ViewAsync("malo", Chroniques.Orsen);

        _service.GetRecent("camille", 10).Viewed.Select(v => v.Element.Name).ShouldBe(["Hautbois"]);
        _service.GetRecent("malo", 10).Viewed.Select(v => v.Element.Name).ShouldBe(["Capitaine Orsen"]);
        _service.GetRecent("nobody", 10).Viewed.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecent_ViewedThenDeleted_LeavesTheListAndComesBackWithRestore()
    {
        await ViewAsync("camille", Chroniques.Hautbois);
        await _service.DeleteElementAsync(Chroniques.Hautbois, "camille", Ct);

        _service.GetRecent("camille", 10).Viewed.ShouldBeEmpty();
        (await _service.RestoreAsync(Chroniques.Hautbois, "camille", Ct)).IsSuccess.ShouldBeTrue();
        _service.GetRecent("camille", 10).Viewed.Select(v => v.Element.Name).ShouldBe(["Hautbois"]);
    }

    [Fact]
    public async Task RecordView_PurgedElement_LeavesNoRow()
    {
        await ViewAsync("camille", Chroniques.Orsen);
        await _service.DeleteElementAsync(Chroniques.Orsen, "camille", Ct);
        await _service.PurgeAsync(null, Ct);

        _service.GetRecent("camille", 10).Viewed.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordView_RootAttachmentDeletedOrUnknown_IsRefused()
    {
        await _service.DeleteElementAsync(Chroniques.Orsen, "camille", Ct);

        (await _service.RecordViewAsync("camille", Roots.Pages.Id, Ct)).IsSuccess.ShouldBeFalse();
        (await _service.RecordViewAsync("camille", Chroniques.IdOf("portrait_ilsa.png"), Ct)).IsSuccess.ShouldBeFalse();
        (await _service.RecordViewAsync("camille", Chroniques.Orsen, Ct)).Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
        (await _service.RecordViewAsync("camille", ElementId.Parse("DEADBEEF00000001"), Ct)).IsSuccess.ShouldBeFalse();
        _service.GetRecent("camille", 10).Viewed.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRecent_Edited_ListsTheLatestChangesWithTheirAuthor()
    {
        // E2E-11-02
        var content = _service.Project.Repository.GetPageContent(Chroniques.Hautbois)!;
        _clock.Advance(TimeSpan.FromMinutes(3));
        await _service.SavePageContentAsync(Chroniques.Hautbois, content.Markdown + "\nUn nouveau puits.", content.Revision, "malo", cancellationToken: Ct);

        var edited = _service.GetRecent("camille", 5).Edited;

        edited[0].Id.ShouldBe(Chroniques.Hautbois);
        (edited[0].UpdatedBy, edited[0].UpdatedAt).ShouldBe(("malo", _clock.GetUtcNow()));
        edited.Select(e => e.UpdatedAt).ShouldBe([.. edited.Select(e => e.UpdatedAt).OrderDescending()]);
        edited.ShouldAllBe(e => e.Kind != ElementKind.Root && e.Kind != ElementKind.Attachment);
    }

    [Fact]
    public async Task GetRecent_Edited_AreSharedAndSkipTheTrash()
    {
        await _service.DeleteElementAsync(Chroniques.Orsen, "camille", Ct);

        _service.GetRecent("malo", 50).Edited.Select(e => e.Id).ShouldNotContain(Chroniques.Orsen);
        _service.GetRecent("malo", 50).Edited.Count.ShouldBe(_service.GetRecent("camille", 50).Edited.Count);
    }

    [Fact]
    public void GetRecent_Limit_IsBounded()
    {
        _service.GetRecent("camille", 3).Edited.Count.ShouldBe(3);
        _service.GetRecent("camille", 0).Edited.Count.ShouldBe(1);
        _service.GetRecent("camille", 100_000).Edited.Count.ShouldBeLessThanOrEqualTo(ProjectService.MaxRecent);
    }
}
