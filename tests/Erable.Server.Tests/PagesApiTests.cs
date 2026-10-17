using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Projects;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

public sealed class PagesApiTests
{
    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<HttpResponseMessage> SaveAsync(TestBackend backend, ElementId projectId, ElementId pageId, string markdown, string? ifMatch)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/projects/{projectId}/pages/{pageId}/content")
        {
            Content = JsonContent.Create(new SavePageContentRequest(markdown), options: ApiJson.Options),
        };
        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        return await backend.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("]", 40_000, "page.content.lineTooLong")]
    [InlineData("> ", 33, "page.content.tooNested")]
    [InlineData("[", 300, "page.content.repeatedCharacter")]
    public async Task SavePageContent_BeyondTheSafetyLimits_Answers422AndKeepsThePage(string unit, int count, string code)
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa", "Avant.");

        using var response = await SaveAsync(backend, project.Id, page.Id, string.Concat(Enumerable.Repeat(unit, count)), $"\"{page.Revision}\"");

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain(code);
        var view = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}"), TestContext.Current.CancellationToken))
            .ReadAsync<PageView>();
        view.Markdown.ShouldBe("Avant.");
    }

    [Fact]
    public async Task GetPage_CreatedWithMarkdown_ReturnsViewWithSubPages()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var ilsa = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne", "## Présentation\n\nCartographe.");
        await backend.CreatePageAsync(project.Id, ilsa.Id, "Dialogues");
        await backend.CreatePageAsync(project.Id, ilsa.Id, "Inventaire");

        var view = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{ilsa.Id}"), TestContext.Current.CancellationToken))
            .ReadAsync<PageView>();

        view.Element.Name.ShouldBe("Ilsa Varenne");
        view.Element.CreatedBy.ShouldBe(TestBackend.AdminLogin);
        view.Markdown.ShouldBe("## Présentation\n\nCartographe.");
        view.SubPages.Select(p => p.Name).ShouldBe(["Dialogues", "Inventaire"]);
        view.Attachments.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPage_Folder_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var folder = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Lieux");

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{folder.Id}"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPageContent_Page_ReturnsRevisionAsETag()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Port.");

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/content"), TestContext.Current.CancellationToken);

        response.Headers.ETag.ShouldBe(new EntityTagHeaderValue("\"1\""));
        (await response.ReadAsync<PageContent>()).ShouldBe(new PageContent("Port.", 1));
    }

    [Fact]
    public async Task SaveContent_MatchingRevision_SavesIndexesAndRecordsRevision()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var orsen = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Capitaine Orsen");
        var ilsa = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne");

        using var response = await SaveAsync(backend, project.Id, ilsa.Id, $"Elle parle peu. Voir [[{orsen.Id}|son allié]].", "\"1\"");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await response.ReadAsync<PageSavedResult>();
        saved.Revision.ShouldBe(2);
        saved.UpdatedBy.ShouldBe(TestBackend.AdminLogin);
        var backlinks = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/elements/{orsen.Id}/backlinks"), TestContext.Current.CancellationToken))
            .ReadAsync<BacklinkItem[]>();
        backlinks.Single().SourceId.ShouldBe(ilsa.Id);
        var service = backend.Services.GetRequiredService<ProjectWorkspace>().Find(project.Id)!;
        service.Project.Repository.CountRevisions(ilsa.Id).ShouldBe(2);
        service.Project.Repository.GetPageContent(ilsa.Id)!.PlainText.ShouldBe("Elle parle peu. Voir son allié.");
    }

    [Fact]
    public async Task SaveContent_StaleRevision_Returns409AndKeepsContent()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois", "Version 1");
        (await SaveAsync(backend, project.Id, page.Id, "Version 2", "\"1\"")).Dispose();

        using var response = await SaveAsync(backend, project.Id, page.Id, "Version concurrente", "\"1\"");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe("page.revisionConflict");
        var content = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/content"), TestContext.Current.CancellationToken))
            .ReadAsync<PageContent>();
        content.ShouldBe(new PageContent("Version 2", 2));
    }

    [Fact]
    public async Task SaveContent_WithoutIfMatch_Returns428()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await SaveAsync(backend, project.Id, page.Id, "x", ifMatch: null);

        response.StatusCode.ShouldBe((HttpStatusCode)428);
        (await response.ProblemCodeAsync()).ShouldBe(Problems.PreconditionRequired);
    }

    [Fact]
    public async Task SaveContent_UnknownPage_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await SaveAsync(backend, project.Id, ElementId.NewRandom(), "x", "\"1\"");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolve_MixedIds_ReturnsStatesNamesAndPaths()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var lieux = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Lieux");
        var hautbois = await backend.CreatePageAsync(project.Id, lieux.Id, "Hautbois");
        var unknown = ElementId.NewRandom();

        var result = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/resolve?ids={hautbois.Id},{unknown.ToString().ToLowerInvariant()}"), TestContext.Current.CancellationToken))
            .ReadAsync<ResolveResult>();

        result.Items.Count.ShouldBe(2);
        result.Items[0].ShouldSatisfyAllConditions(
            i => i.State.ShouldBe(ResolvedState.Existing),
            i => i.Name.ShouldBe("Hautbois"),
            i => i.Kind.ShouldBe(ElementKind.Page),
            i => i.Path.Select(p => p.Name).ShouldBe(["Pages", "Lieux"]));
        result.Items[1].ShouldSatisfyAllConditions(i => i.Id.ShouldBe(unknown), i => i.State.ShouldBe(ResolvedState.Unknown));
    }

    [Fact]
    public async Task Resolve_InvalidId_Returns400()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/resolve?ids=XYZ"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Resolve_TwoHundredIds_AnswersInOneCall()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var ids = Enumerable.Range(0, 200).Select(_ => ElementId.NewRandom()).ToList();

        var result = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/resolve?ids={string.Join(',', ids)}"), TestContext.Current.CancellationToken))
            .ReadAsync<ResolveResult>();

        result.Items.Select(i => i.Id).ShouldBe(ids);
    }

    [Fact]
    public async Task SaveContent_RealisticPage_IsFast()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var target = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Cible");
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne");
        var markdown = new StringBuilder();
        for (var i = 0; i < 40; i++)
        {
            markdown.Append(CultureInfoInvariant($"## Section {i}\n\nTexte de la section, avec [[{target.Id}|un lien]] et du **gras**.\n\n- point\n- autre point\n\n"));
        }

        var durations = new List<double>();
        for (var revision = 1; revision <= 20; revision++)
        {
            var stopwatch = Stopwatch.StartNew();
            using var response = await SaveAsync(backend, project.Id, page.Id, markdown + revision.ToString(System.Globalization.CultureInfo.InvariantCulture), $"\"{revision}\"");
            durations.Add(stopwatch.Elapsed.TotalMilliseconds);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Budget 100 ms (docs/testing.md §11.1, measured formally in M3-06); generous bound against CI noise.
        durations.Order().ElementAt(durations.Count / 2).ShouldBeLessThan(250);
    }

    private static string CultureInfoInvariant(FormattableString value) => FormattableString.Invariant(value);
}
