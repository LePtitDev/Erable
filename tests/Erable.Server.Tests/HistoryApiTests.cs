using System.Net;
using System.Net.Http.Json;
using Erable.Core;
using Erable.Core.History;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing.Reference;

namespace Erable.Server.Tests;

/// <summary>Revision history over HTTP (F13), on the reference project.</summary>
public sealed class HistoryApiTests
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

    private static async Task SaveAsync(TestBackend backend, string root, string markdown)
    {
        var content = await (await backend.Client.GetAsync(Url($"{root}/pages/{Chroniques.Hautbois}/content"), Ct)).ReadAsync<PageContent>();
        using var request = new HttpRequestMessage(HttpMethod.Put, Url($"{root}/pages/{Chroniques.Hautbois}/content"))
        {
            Content = JsonContent.Create(new SavePageContentRequest(markdown)),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{content.Revision}\"");
        (await backend.Client.SendAsync(request, Ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task History_ListCompareAndRestore_RoundTrip()
    {
        // E2E-13-01 to E2E-13-03
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        await SaveAsync(backend, root, "un");
        await SaveAsync(backend, root, "un\ndeux");
        await SaveAsync(backend, root, "un\ndeux\ntrois");

        var list = await (await backend.Client.GetAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions"), Ct)).ReadAsync<RevisionItem[]>();
        var diff = await (await backend.Client.GetAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions/diff?from=4&to=1"), Ct)).ReadAsync<RevisionDiffView>();
        var one = await (await backend.Client.GetAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions/2"), Ct)).ReadAsync<RevisionView>();
        using var restore = await backend.Client.PostAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions/2/restore"), null, Ct);
        var restored = await restore.ReadAsync<RevisionRestoredView>();
        var after = await (await backend.Client.GetAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions"), Ct)).ReadAsync<RevisionItem[]>();

        list.Select(r => r.Revision).ShouldBe([4, 3, 2, 1]);
        list[3].Changes.ShouldBe(["created"]);
        list[0].AuthorName.ShouldNotBeNullOrEmpty();
        (diff.From.Revision, diff.To.Revision).ShouldBe((1, 4));
        diff.Text.Select(r => r.Kind).ShouldContain(DiffRowKind.Removed);
        diff.Text.Where(r => r.Kind != DiffRowKind.Removed).Select(r => r.New?.Text).ShouldContain("trois");
        one.Markdown.ShouldBe("un");
        restored.Revision.ShouldBe(5);
        after.Length.ShouldBe(5);
        after[0].RestoredFrom.ShouldBe(2);
    }

    [Theory]
    [InlineData("revisions/99", HttpStatusCode.NotFound)]
    [InlineData("revisions/diff?from=1&to=99", HttpStatusCode.NotFound)]
    [InlineData("revisions/diff?from=1", HttpStatusCode.BadRequest)]
    public async Task History_UnknownRevision_IsRefused(string path, HttpStatusCode status)
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/elements/{Chroniques.Hautbois}/{path}"), Ct);

        response.StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task Restore_UnknownRevisionOrElement_IsRefused()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var unknown = await backend.Client.PostAsync(Url($"{root}/elements/{Chroniques.Hautbois}/revisions/99/restore"), null, Ct);
        using var folder = await backend.Client.PostAsync(Url($"{root}/elements/{Chroniques.Pnj}/revisions/1/restore"), null, Ct);
        using var missing = await backend.Client.GetAsync(Url($"{root}/elements/DEADBEEF00000001/revisions"), Ct);

        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        folder.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
