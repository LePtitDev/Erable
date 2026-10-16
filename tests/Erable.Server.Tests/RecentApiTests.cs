using System.Net;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing.Reference;

namespace Erable.Server.Tests;

/// <summary>Recently viewed and edited elements over HTTP (M3-04, F11).</summary>
public sealed class RecentApiTests
{
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

    [Fact]
    public async Task RecordView_ThenGetRecent_ListsTheViewsNewestFirst()
    {
        // E2E-11-01
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        foreach (var id in new[] { Chroniques.Pnj, Chroniques.Hautbois, Chroniques.Niveau })
        {
            using var response = await backend.Client.PostJsonAsync($"{root}/me/views", new RecordViewRequest(id));
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            await Task.Delay(5, Ct);
        }

        var recent = await (await backend.Client.GetAsync(Url($"{root}/me/recent"), Ct)).ReadAsync<RecentActivityView>();

        recent.Viewed.Select(v => v.Name).ShouldBe(["Niveau", "Hautbois", "PNJ"]);
        recent.Viewed.Select(v => v.Kind).ShouldBe([ElementKind.PropertyDefinition, ElementKind.Page, ElementKind.Category]);
        recent.Edited.ShouldNotBeEmpty();
        recent.Edited[0].EditedByName.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RecordView_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync($"{root}/me/views", new RecordViewRequest(ElementId.Parse("DEADBEEF00000001")));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetRecent_LimitAndUnknownProject_AreHandled()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var recent = await (await backend.Client.GetAsync(Url($"{root}/me/recent?limit=2"), Ct)).ReadAsync<RecentActivityView>();
        using var missing = await backend.Client.GetAsync(Url("/api/v1/projects/DEADBEEF00000001/me/recent"), Ct);

        recent.Edited.Count.ShouldBe(2);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
