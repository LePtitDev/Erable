using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Tests.Infrastructure;
using Erable.Testing.Reference;

namespace Erable.Server.Tests;

/// <summary>The <c>/_test</c> endpoints used by the e2e tests (docs/features.md §1.4).</summary>
public sealed class TestEndpointsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<TestBackend> StartAsync() =>
        TestBackend.StartAsync(o => o with { TestMode = true });

    private static async Task<(ElementId ProjectId, string Cookie)> ResetAsync(TestBackend backend, string slot, string fixture = "chroniques")
    {
        using var response = await backend.Client.PostAsync(new Uri($"/_test/reset?slot={slot}&fixture={fixture}", UriKind.Relative), null, Ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);
        var cookie = response.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        return (ElementId.Parse(body!["projectId"]!.GetValue<string>()), cookie);
    }

    private static async Task<HttpResponseMessage> GetAsync(TestBackend backend, string url, string? cookie = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        return await backend.Client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task Reset_Chroniques_OpensIlsaByItsFrozenId()
    {
        await using var backend = await StartAsync();
        var (projectId, _) = await ResetAsync(backend, "f01");

        var view = await (await GetAsync(backend, $"/api/v1/projects/{projectId}/pages/{Chroniques.Ilsa}")).ReadAsync<PageView>();

        view.Element.Name.ShouldBe("Ilsa Varenne");
        view.Element.Path.Select(p => p.Name).ShouldBe(["Pages", "Personnages", "PNJ"]);
        view.Element.UpdatedByName.ShouldBe("Camille R.");
        view.Element.UpdatedAt.ShouldBe(Chroniques.Now.AddHours(-2));
        view.SubPages.Select(s => s.Name).ShouldBe(["Dialogues", "Inventaire"]);
        view.Attachments.Single().FileName.ShouldBe("portrait_ilsa.png");
    }

    [Fact]
    public async Task Reset_TwoSlots_GetIsolatedCopiesAndProfiles()
    {
        await using var backend = await StartAsync();
        var (first, firstCookie) = await ResetAsync(backend, "f01");
        var (second, secondCookie) = await ResetAsync(backend, "f02");

        first.ShouldNotBe(second);
        using var rename = await backend.Client.PatchJsonAsync($"/api/v1/projects/{first}/elements/{Chroniques.Orsen}", new UpdateElementRequest("Commandant Orsen"));
        rename.EnsureSuccessStatusCode();
        var other = await (await GetAsync(backend, $"/api/v1/projects/{second}/elements/{Chroniques.Orsen}")).ReadAsync<ElementDetails>();
        other.Name.ShouldBe("Capitaine Orsen");
        var recentOfFirst = await (await GetAsync(backend, "/api/v1/me/recent-projects", firstCookie)).ReadAsync<RecentProjectItem[]>();
        var recentOfSecond = await (await GetAsync(backend, "/api/v1/me/recent-projects", secondCookie)).ReadAsync<RecentProjectItem[]>();
        recentOfFirst.Select(r => r.ProjectId).ShouldBe([first]);
        recentOfSecond.Select(r => r.ProjectId).ShouldBe([second]);
    }

    [Fact]
    public async Task Reset_SameSlotAgain_ClosesThePreviousCopy()
    {
        await using var backend = await StartAsync();
        var (previous, _) = await ResetAsync(backend, "f01");

        var (current, _) = await ResetAsync(backend, "f01");

        (await GetAsync(backend, $"/api/v1/projects/{previous}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await GetAsync(backend, $"/api/v1/projects/{current}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_IsFast()
    {
        await using var backend = await StartAsync();
        await ResetAsync(backend, "warmup");

        var stopwatch = Stopwatch.StartNew();
        await ResetAsync(backend, "f01");

        // docs/features.md §1.4: < 100 ms; generous bound for slow CI disks.
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(1000);
    }

    [Fact]
    public async Task Reset_UnknownFixture_Returns404()
    {
        await using var backend = await StartAsync();

        using var response = await backend.Client.PostAsync(new Uri("/_test/reset?slot=f01&fixture=..%2Flegacy", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Clock_Set_StampsTheNextSave()
    {
        await using var backend = await StartAsync();
        var (projectId, _) = await ResetAsync(backend, "f01");
        var at = new DateTimeOffset(2026, 12, 24, 18, 0, 0, TimeSpan.Zero);

        (await backend.Client.PostJsonAsync("/_test/clock", new { now = at })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var page = await backend.CreatePageAsync(projectId, Erable.Core.Elements.Roots.Pages.Id, "Lame de givre");

        (page.CreatedAt - at).ShouldBeLessThan(TimeSpan.FromMinutes(1));
        page.CreatedAt.ShouldBeGreaterThanOrEqualTo(at);
    }

    [Fact]
    public async Task Clock_Default_IsTheReferenceDate()
    {
        await using var backend = await StartAsync();
        var clock = (TimeProvider)backend.Services.GetService(typeof(TimeProvider))!;

        (clock.GetUtcNow() - Chroniques.Now).ShouldBeLessThan(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task Blobs_SameFileAttachedTwice_OneBlob()
    {
        await using var backend = await StartAsync();
        var (projectId, _) = await ResetAsync(backend, "f01");
        var before = await (await GetAsync(backend, $"/_test/blobs?projectId={projectId}")).ReadAsync<JsonObject>();
        var png = await File.ReadAllBytesAsync(Path.Combine(Chroniques.AssetsDirectory, "portrait_ilsa.png"), Ct);

        (await backend.UploadAsync(projectId, Chroniques.Hautbois, "copie.png", png)).EnsureSuccessStatusCode();

        var after = await (await GetAsync(backend, $"/_test/blobs?projectId={projectId}")).ReadAsync<JsonObject>();
        after["hashes"]!.AsArray().Count.ShouldBe(before["hashes"]!.AsArray().Count);
    }

    [Fact]
    public async Task Limits_Lowered_RefusesALargerFile()
    {
        await using var backend = await StartAsync();
        var (projectId, _) = await ResetAsync(backend, "f01");
        (await backend.Client.PostJsonAsync("/_test/limits", new { maxAttachmentSize = 100 })).EnsureSuccessStatusCode();

        using var response = await backend.UploadAsync(projectId, Chroniques.Hautbois, "gros.bin", new byte[500]);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Endpoints_WithoutTestMode_AreAbsent()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { TestMode = false });

        using var response = await backend.Client.PostAsync(new Uri("/_test/reset?slot=f01", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        ErableHost.TestEndpointsCompiled.ShouldBeTrue();
    }
}
