using System.Net;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

public sealed class TreeApiTests
{
    private static async Task<ProjectTree> GetTreeAsync(TestBackend backend, ElementId projectId) =>
        await (await backend.Client.GetAsync(new Uri($"/api/v1/projects/{projectId}/tree", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<ProjectTree>();

    [Fact]
    public async Task GetTree_NewProject_HasFourRootsInContentLanguage()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync(language: "de");

        var tree = await GetTreeAsync(backend, project.Id);

        tree.Nodes.Select(n => (n.Kind, n.Name, n.ParentId))
            .ShouldBe([(ElementKind.Root, "Seiten", null), (ElementKind.Root, "Kategorien", null), (ElementKind.Root, "Eigenschaften", null), (ElementKind.Root, "Verknüpfungen", (ElementId?)null)]);
        tree.Counts.ShouldBe(new RootCounts(0, 0, 0, 0));
    }

    [Fact]
    public async Task CreateFolderAndPage_UnderPages_AppearInTreeAndCountsExcludeFolders()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        var folder = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Objets");
        var page = await backend.CreatePageAsync(project.Id, folder.Id, "Lame de givre");

        page.Path.Select(p => p.Name).ShouldBe(["Pages", "Objets"]);
        var tree = await GetTreeAsync(backend, project.Id);
        tree.Nodes.ShouldContain(n => n.Id == page.Id && n.ParentId == folder.Id);
        tree.Counts.Pages.ShouldBe(1);
    }

    [Fact]
    public async Task CreatePage_UnderCategoriesRoot_Returns422CrossRoot()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.PostJsonAsync($"/api/v1/projects/{project.Id}/pages", new CreatePageRequest(Roots.Categories.Id, "Perdue"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public async Task CreatePage_ParentIdAsJsonNumber_Returns400()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        using var content = new StringContent("""{"parentId":1,"name":"Nombre"}""", System.Text.Encoding.UTF8, "application/json");

        using var response = await backend.Client.PostAsync(new Uri($"/api/v1/projects/{project.Id}/pages", UriKind.Relative), content, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PatchElement_Rename_KeepsReferencingMarkdownUnchanged()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var orsen = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Capitaine Orsen");
        var markdown = $"Allié de [[{orsen.Id}]].";
        var ilsa = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne", markdown);

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{orsen.Id}", new UpdateElementRequest(Name: "Commandant Orsen"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renamed = await response.ReadAsync<ElementDetails>();
        renamed.Name.ShouldBe("Commandant Orsen");
        renamed.Revision.ShouldBe(2);
        var content = await (await backend.Client.GetAsync(new Uri($"/api/v1/projects/{project.Id}/pages/{ilsa.Id}/content", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<PageContent>();
        content.Markdown.ShouldBe(markdown);
        content.Revision.ShouldBe(1);
    }

    [Fact]
    public async Task PatchElement_MoveToOtherFolder_UpdatesPath()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var lieux = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Lieux");
        var factions = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Factions");
        var hautbois = await backend.CreatePageAsync(project.Id, lieux.Id, "Hautbois");

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{hautbois.Id}", new UpdateElementRequest(ParentId: factions.Id));

        (await response.ReadAsync<ElementDetails>()).Path.Select(p => p.Name).ShouldBe(["Pages", "Factions"]);
    }

    [Fact]
    public async Task PatchElement_Reorder_IsKeptInTree()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var hautbois = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var col = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Col gelé");

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{col.Id}", new UpdateElementRequest(Index: 0));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tree = await GetTreeAsync(backend, project.Id);
        tree.Nodes.Where(n => n.ParentId == Roots.Pages.Id).Select(n => n.Id).ShouldBe([col.Id, hautbois.Id]);
    }

    [Fact]
    public async Task PatchElement_MoveUnderOtherRoot_Returns422WithStableCode()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{page.Id}", new UpdateElementRequest(ParentId: Roots.Categories.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public async Task PatchElement_UnderOwnSubPage_Returns422()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var ilsa = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Ilsa Varenne");
        var dialogues = await backend.CreatePageAsync(project.Id, ilsa.Id, "Dialogues");

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{ilsa.Id}", new UpdateElementRequest(ParentId: dialogues.Id));

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MoveIntoDescendant);
    }

    [Fact]
    public async Task PatchElement_RenameRoot_Returns422()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{Roots.Pages.Id}", new UpdateElementRequest(Name: "Autre"));

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public async Task PatchElement_Unknown_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.PatchJsonAsync($"/api/v1/projects/{project.Id}/elements/{ElementId.NewRandom()}", new UpdateElementRequest(Name: "x"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.ElementNotFound);
    }

    [Fact]
    public async Task GetElement_Root_IsLabelledAndHasNoPath()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync(language: "es");

        var root = await (await backend.Client.GetAsync(new Uri($"/api/v1/projects/{project.Id}/elements/{Roots.Pages.Id}", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<ElementDetails>();

        root.Name.ShouldBe("Páginas");
        root.Path.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTree_SiblingsWithSameName_AreFlagged()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "hautbois");
        await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Col gelé");

        var tree = await GetTreeAsync(backend, project.Id);

        tree.Nodes.Where(n => n.DuplicateName).Select(n => n.Name).ShouldBe(["Hautbois", "hautbois"], ignoreOrder: true);
    }
}
