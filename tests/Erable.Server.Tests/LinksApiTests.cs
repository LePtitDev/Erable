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

/// <summary>Link types and typed links over HTTP (M2-07), on the reference project.</summary>
public sealed class LinksApiTests
{
    private static readonly ElementId MentorDe = Chroniques.IdOf("Mentor de");
    private static readonly ElementId AllieDe = Chroniques.IdOf("Allié de");

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

    private static Task<HttpResponseMessage> PostAsync<T>(TestBackend backend, string url, T body) =>
        backend.Client.PostAsJsonAsync(url, body, ApiJson.Options, Ct);

    [Fact]
    public async Task ListLinkTypes_ReferenceProject_HasNamesInversesAndCounts()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var types = await GetAsync<LinkTypeItem[]>(backend, $"{root}/link-types");

        types.Select(t => (t.Name, t.InverseName, t.LinkCount)).ShouldBe(
            [("Allié de", "Allié de", 1), ("Mentor de", "Élève de", 0), ("Situé à", "Lieu de", 1)]);
    }

    [Fact]
    public async Task CreateLinkType_ReturnsTheViewUnderTheLinksRoot()
    {
        // E2E-09-01
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PostAsync(backend, $"{root}/link-types",
            new CreateLinkTypeRequest(Roots.Links.Id, "Rival de", Description: "Deux rivaux.", InverseName: "Rival de"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var view = await response.ReadAsync<LinkTypeView>();
        (view.Element.Name, view.Element.Kind, view.Description, view.InverseName, view.LinkCount)
            .ShouldBe(("Rival de", ElementKind.LinkType, "Deux rivaux.", "Rival de", 0));
        view.Element.Path.Select(p => p.Name).ShouldBe(["Liens"]);
    }

    [Fact]
    public async Task UpdateLinkType_ChangesTheInverse()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PutAsJsonAsync(
            $"{root}/link-types/{MentorDe}", new UpdateLinkTypeRequest("Forme.", "Disciple de"), ApiJson.Options, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<LinkTypeView>()).InverseName.ShouldBe("Disciple de");
    }

    [Fact]
    public async Task GetLinkType_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/link-types/{Chroniques.Ilsa}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateLink_ThenSeenFromBothPages()
    {
        // E2E-09-02
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PostAsync(backend, $"{root}/links", new CreateLinkRequest(Chroniques.Ilsa, MentorDe, Chroniques.Orsen));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var ilsa = await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Ilsa}/links");
        ilsa.Outgoing.ShouldContain(l => l.Label == "Mentor de" && l.PageName == "Capitaine Orsen");
        var orsen = await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Orsen}/links");
        orsen.Incoming.ShouldContain(l => l.Label == "Élève de" && l.PageName == "Ilsa Varenne");
    }

    [Fact]
    public async Task GetPageLinks_Hautbois_HasTheIncomingLocationLink()
    {
        // E2E-09-03
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var links = await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Hautbois}/links");

        links.Incoming.Select(l => (l.Label, l.PageName)).ShouldBe([("Lieu de", "Ilsa Varenne")]);
        links.Outgoing.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateLink_Duplicate_Returns409()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PostAsync(backend, $"{root}/links", new CreateLinkRequest(Chroniques.Ilsa, AllieDe, Chroniques.Orsen));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>().ShouldBe(ErrorCodes.LinkDuplicate);
    }

    [Fact]
    public async Task CreateLink_ToItself_Returns422()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PostAsync(backend, $"{root}/links", new CreateLinkRequest(Chroniques.Ilsa, MentorDe, Chroniques.Ilsa));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!["code"]!.GetValue<string>().ShouldBe(ErrorCodes.LinkSelf);
    }

    [Fact]
    public async Task DeleteLink_DisappearsFromBothPages()
    {
        // E2E-09-04
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var link = (await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Ilsa}/links")).Outgoing.Single(l => l.Label == "Allié de");

        using var response = await backend.Client.DeleteAsync(Url($"{root}/links/{link.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Ilsa}/links")).Outgoing.ShouldNotContain(l => l.Label == "Allié de");
        (await GetAsync<PageLinksView>(backend, $"{root}/pages/{Chroniques.Orsen}/links")).Incoming.ShouldBeEmpty();
    }

    [Fact]
    public async Task DeleteLink_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.DeleteAsync(Url($"{root}/links/{Chroniques.Orsen}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPageLinks_NotAPage_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/pages/{Chroniques.Pnj}/links"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
