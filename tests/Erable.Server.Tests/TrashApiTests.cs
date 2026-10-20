using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing.Reference;

namespace Erable.Server.Tests;

/// <summary>Deleting, the trash, restoring and emptying over HTTP, on the reference project.</summary>
public sealed class TrashApiTests
{
    private static readonly ElementId Lieux = Chroniques.IdOf("Lieux");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<(TestBackend Backend, string Base)> StartAsync()
    {
        var backend = await TestBackend.StartAsync();
        var folder = Path.Combine(backend.Options.DataDirectory!, "projects", "chroniques");
        using (Chroniques.Write(folder))
        {
        }

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder));
        response.EnsureSuccessStatusCode();
        return (backend, $"/api/v1/projects/{Chroniques.ProjectId}");
    }

    private static async Task<T> GetAsync<T>(TestBackend backend, string url) =>
        await (await backend.Client.GetAsync(Url(url), Ct)).ReadAsync<T>();

    private static async Task DeleteOkAsync(TestBackend backend, string root, ElementId id)
    {
        using var response = await backend.Client.DeleteAsync(Url($"{root}/elements/{id}"), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetImpact_PageWithSubPages_CountsThemAndListsTheMentions()
    {
        // E2E-12-01, E2E-12-03
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var orsen = await GetAsync<DeleteImpactView>(backend, $"{root}/elements/{Chroniques.Orsen}/impact");
        var ilsa = await GetAsync<DeleteImpactView>(backend, $"{root}/elements/{Chroniques.Ilsa}/impact");

        orsen.ReferencedBy.Select(i => i.Name).ShouldBe(["Ilsa Varenne"]);
        ilsa.SubPages.ShouldBe(2);
    }

    [Fact]
    public async Task GetImpact_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/elements/DEADBEEF00000001/impact"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Page_GoesToTheTrashAndItsReferencesAreResolvedAsDeleted()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        await DeleteOkAsync(backend, root, Chroniques.Orsen);

        var trash = await GetAsync<TrashEntryView[]>(backend, $"{root}/trash");
        var entry = trash.Single();
        (entry.Id, entry.Name, entry.Kind, entry.ParentAvailable).ShouldBe((Chroniques.Orsen, "Capitaine Orsen", ElementKind.Page, true));
        entry.Path.Select(p => p.Name).ShouldBe(["Pages", "Personnages", "PNJ"]);
        var resolved = await GetAsync<ResolveResult>(backend, $"{root}/resolve?ids={Chroniques.Orsen}");
        resolved.Items.Single().State.ShouldBe(ResolvedState.Deleted);
        (await GetAsync<ElementDetails>(backend, $"{root}/elements/{Chroniques.Orsen}")).Deleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Delete_Root_Returns422()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url($"{root}/elements/{Roots.Pages.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>().ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public async Task Delete_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url($"{root}/elements/DEADBEEF00000001"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_Entry_ReturnsTheElementToItsParent()
    {
        // E2E-12-02
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        await DeleteOkAsync(backend, root, Chroniques.Orsen);

        using var response = await backend.Client.PostAsync(Url($"{root}/trash/{Chroniques.Orsen}/restore"), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var restored = await response.ReadAsync<RestoreView>();
        (restored.Id, restored.ParentName, restored.Relocated).ShouldBe((Chroniques.Orsen, "PNJ", false));
        (await GetAsync<TrashEntryView[]>(backend, $"{root}/trash")).ShouldBeEmpty();
        (await GetAsync<ElementDetails>(backend, $"{root}/elements/{Chroniques.Orsen}")).Deleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Restore_ParentGone_ReportsTheRelocation()
    {
        // E2E-12-05
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        await DeleteOkAsync(backend, root, Chroniques.Hautbois);
        await DeleteOkAsync(backend, root, Lieux);
        using (var purge = await backend.Client.DeleteAsync(Url($"{root}/trash/{Lieux}"), Ct))
        {
            purge.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await backend.Client.PostAsync(Url($"{root}/trash/{Chroniques.Hautbois}/restore"), null, Ct);

        var restored = await response.ReadAsync<RestoreView>();
        (restored.Relocated, restored.ParentId).ShouldBe((true, Roots.Pages.Id));
    }

    [Fact]
    public async Task Restore_NotInTheTrash_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostAsync(Url($"{root}/trash/{Chroniques.Orsen}/restore"), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EmptyTrash_RemovesEveryEntryForGood()
    {
        // E2E-12-06
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        await DeleteOkAsync(backend, root, Chroniques.Orsen);
        await DeleteOkAsync(backend, root, Chroniques.Ilsa);

        using var response = await backend.Client.DeleteAsync(Url($"{root}/trash"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<PurgeView>()).Removed.ShouldBe(5);
        (await GetAsync<TrashEntryView[]>(backend, $"{root}/trash")).ShouldBeEmpty();
        using var restore = await backend.Client.PostAsync(Url($"{root}/trash/{Chroniques.Orsen}/restore"), null, Ct);
        restore.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var resolved = await GetAsync<ResolveResult>(backend, $"{root}/resolve?ids={Chroniques.Orsen}");
        resolved.Items.Single().State.ShouldBe(ResolvedState.Unknown);
    }

    [Fact]
    public async Task PurgeEntry_NotInTheTrash_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url($"{root}/trash/{Chroniques.Orsen}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
