using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

/// <summary>Two editors of one page (M4-05, F14.R5): merged on the server, or both versions handed back.</summary>
public sealed class ConcurrentEditingApiTests
{
    private const string Page = "## Présentation\n\nMéfiante envers les soldats.\n\n## Histoire\n\nNée à Hautbois.\n\n## Rôle\n\nMarchande de cartes.\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(TestBackend Backend, ProjectSummary Project, ElementDetails Page, HttpClient Camille, HttpClient Malo)> StartAsync()
    {
        var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync("Chroniques");
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne", Page);
        var camille = backend.ClientFor("camille");
        var malo = backend.ClientFor("malo");
        backend.Accounts.Store.SetMember(project.Id, "camille", ProjectRole.Editor);
        backend.Accounts.Store.SetMember(project.Id, "malo", ProjectRole.Editor);
        return (backend, project, page, camille, malo);
    }

    private static async Task<HttpResponseMessage> SaveAsync(HttpClient client, ElementId project, ElementId page, string markdown, long revision)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"/api/v1/projects/{project}/pages/{page}/content", UriKind.Relative))
        {
            Content = JsonContent.Create(new SavePageContentRequest(markdown), options: ApiJson.Options),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{revision}\"");
        return await client.SendAsync(request, Ct);
    }

    private static async Task<string> CurrentMarkdownAsync(HttpClient client, ElementId project, ElementId page) =>
        (await (await client.GetAsync(new Uri($"/api/v1/projects/{project}/pages/{page}/content", UriKind.Relative), Ct)).ReadAsync<PageContent>()).Markdown;

    [Fact]
    public async Task Put_StartedBeforeAnotherSaveOnAnotherSection_ReturnsTheMergedPage()
    {
        // E2E-14-07
        var (backend, project, page, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;
        using var first = await SaveAsync(camille, project.Id, page.Id, Page.Replace("les soldats", "tous les soldats", StringComparison.Ordinal), 1);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await SaveAsync(malo, project.Id, page.Id, Page.Replace("Née à Hautbois", "Née à Hautbois-sur-Brume", StringComparison.Ordinal), 1);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await response.ReadAsync<PageSavedResult>();
        saved.Revision.ShouldBe(3);
        saved.UpdatedBy.ShouldBe("malo");
        var merged = saved.Merged.ShouldNotBeNull();
        merged.MergedWith.ShouldBe([new AuthorItem("camille", "Camille")]);
        merged.Markdown.ShouldContain("tous les soldats");
        merged.Markdown.ShouldContain("Hautbois-sur-Brume");
        (await CurrentMarkdownAsync(malo, project.Id, page.Id)).ShouldBe(merged.Markdown);
        response.Headers.ETag!.Tag.ShouldBe("\"3\"");
    }

    [Fact]
    public async Task Put_UpToDate_HasNoMergeInfo()
    {
        var (backend, project, page, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;

        using var response = await SaveAsync(camille, project.Id, page.Id, Page + "\nFin.", 1);

        (await response.ReadAsync<PageSavedResult>()).Merged.ShouldBeNull();
    }

    [Fact]
    public async Task Put_OnTheSameSentence_Returns409WithTheConflictingBlocks()
    {
        // E2E-14-08
        var (backend, project, page, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;
        var theirs = Page.Replace("Née à Hautbois.", "Ilsa est née à Hautbois.", StringComparison.Ordinal);
        var mine = Page.Replace("Née à Hautbois.", "Ilsa rejoint l’Ordre.", StringComparison.Ordinal);
        (await SaveAsync(camille, project.Id, page.Id, theirs, 1)).Dispose();

        using var response = await SaveAsync(malo, project.Id, page.Id, mine, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        body["code"]!.GetValue<string>().ShouldBe("page.revisionConflict");
        body["current"]!["revision"]!.GetValue<long>().ShouldBe(2);
        body["current"]!["markdown"]!.GetValue<string>().ShouldBe(theirs);
        var blocks = body["blocks"]!.AsArray();
        var conflict = blocks.Single(b => b!["kind"]!.GetValue<string>() == "conflict")!;
        conflict["mine"]!.GetValue<string>().ShouldBe("Ilsa rejoint l’Ordre.\n");
        conflict["theirs"]!.GetValue<string>().ShouldBe("Ilsa est née à Hautbois.\n");
        conflict["base"]!.GetValue<string>().ShouldBe("Née à Hautbois.\n");
        body["mergedWith"]![0]!["name"]!.GetValue<string>().ShouldBe("Camille");
        (await CurrentMarkdownAsync(malo, project.Id, page.Id)).ShouldBe(theirs);
    }

    [Fact]
    public async Task Put_StaleSaveAfterResolution_Succeeds()
    {
        var (backend, project, page, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;
        (await SaveAsync(camille, project.Id, page.Id, Page.Replace("Née à Hautbois.", "A.", StringComparison.Ordinal), 1)).Dispose();
        var mine = Page.Replace("Née à Hautbois.", "B.", StringComparison.Ordinal);
        using var conflict = await SaveAsync(malo, project.Id, page.Id, mine, 1);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        using var resolved = await SaveAsync(malo, project.Id, page.Id, mine, 2);

        resolved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CurrentMarkdownAsync(malo, project.Id, page.Id)).ShouldBe(mine);
    }

    [Fact]
    public async Task ParallelWritesOnDifferentPages_LoseNothing()
    {
        // « N écritures parallèles sur des pages différentes → aucune perte »
        var (backend, project, _, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;
        const int Count = 24;
        var pages = new List<ElementDetails>();
        for (var i = 0; i < Count; i++)
        {
            pages.Add(await backend.CreatePageAsync(project.Id, Roots.Pages.Id, $"Page {i}", "Début."));
        }

        await Parallel.ForEachAsync(pages.Select((p, i) => (p, i)), Ct, async (job, token) =>
        {
            var client = job.i % 2 == 0 ? camille : malo;
            using var response = await SaveAsync(client, project.Id, job.p.Id, $"Début.\n\nRédaction {job.i}.", 1);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"page {job.i}");
            token.ThrowIfCancellationRequested();
        });

        foreach (var (page, i) in pages.Select((p, i) => (p, i)))
        {
            (await CurrentMarkdownAsync(backend.Client, project.Id, page.Id)).ShouldBe($"Début.\n\nRédaction {i}.");
        }
    }

    [Fact]
    public async Task ParallelWritesOnOnePage_AllEditsSurviveOrAreReported()
    {
        // Eight editors from the same revision, each in its own section: the page ends with every edit.
        var (backend, project, page, camille, malo) = await StartAsync();
        await using var _ = backend;
        using var _camille = camille;
        using var _malo = malo;
        const int Count = 8;
        var sections = string.Concat(Enumerable.Range(0, Count).Select(i => $"## Section {i}\n\nTexte {i}.\n\n"));
        (await SaveAsync(backend.Client, project.Id, page.Id, sections, 1)).Dispose();

        await Parallel.ForEachAsync(Enumerable.Range(0, Count), Ct, async (i, _) =>
        {
            var edited = sections.Replace($"Texte {i}.", $"Texte {i} modifié.", StringComparison.Ordinal);
            using var response = await SaveAsync(i % 2 == 0 ? camille : malo, project.Id, page.Id, edited, 2);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"writer {i}");
        });

        var final = await CurrentMarkdownAsync(backend.Client, project.Id, page.Id);
        for (var i = 0; i < Count; i++)
        {
            final.ShouldContain($"Texte {i} modifié.");
        }
    }
}
