using System.Net;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Server.Tests;

/// <summary>Search over HTTP (M3-01, F10), on the reference project with the clock of the test mode.</summary>
public sealed class SearchApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    private static async Task<(TestBackend Backend, string Base)> StartAsync()
    {
        var backend = await TestBackend.StartAsync(o => o with { TimeProvider = new FakeTimeProvider(Chroniques.Now) });
        var folder = Path.Combine(backend.Options.DataDirectory!, "projects", "chroniques");
        using (Chroniques.Write(folder))
        {
        }

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder));
        response.EnsureSuccessStatusCode();
        return (backend, $"/api/v1/projects/{Chroniques.ProjectId}");
    }

    private static async Task<SearchResultPage> SearchAsync(TestBackend backend, string root, string query)
    {
        var response = await backend.Client.GetAsync(Url($"{root}/search?{query}"), Ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await response.Content.ReadAsStringAsync(Ct));
        }

        return await response.ReadAsync<SearchResultPage>();
    }

    private static string[] Names(SearchResultPage page) => [.. page.Hits.Select(h => h.Name)];

    [Fact]
    public async Task Search_Text_FindsAccentedNamesAndReturnsHighlightedSegments()
    {
        // E2E-10-02
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var page = await SearchAsync(backend, root, "q=col%20gele");

        Names(page).ShouldContain("Le Col gelé");
        Names(page).ShouldContain("Col gelé");
        var ilsa = page.Hits.Single(h => h.Id == Chroniques.Ilsa);
        ilsa.Excerpt.Where(s => s.Hit).Select(s => s.Text).ShouldContain("Col gelé");
        ilsa.Path.Select(p => p.Name).ShouldBe(["Pages", "Personnages", "PNJ"]);
        ilsa.Categories.Select(c => c.Name).ShouldBe(["PNJ", "Marchand"]);
        page.Facets.Single(f => f.Kind == ElementKind.Page).Count.ShouldBeGreaterThan(2);
    }

    [Fact]
    public async Task Search_QuickSearch_PutsThePageNamedLikeTheTextFirst()
    {
        // E2E-10-01
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var page = await SearchAsync(backend, root, "q=ilsa&nameOnly=true");

        page.Hits[0].Id.ShouldBe(Chroniques.Ilsa);
    }

    [Fact]
    public async Task Search_KindAndCategoryWithSubCategories_ListsThePages()
    {
        // E2E-10-03
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var page = await SearchAsync(backend, root, $"kind=page&category={Chroniques.Personnage}&subCategories=true&sort=name");

        Names(page).ShouldBe(["Capitaine Orsen", "Ilsa Varenne"]);
    }

    [Fact]
    public async Task Search_PropertyConditions_CompareInheritedAndNumericValues()
    {
        // E2E-10-04, E2E-10-05
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var hostile = Chroniques.IdOf("Hostile");

        var inherited = await SearchAsync(backend, root, $"where={hostile}:eq:false&sort=name");
        var numeric = await SearchAsync(backend, root, $"where={Chroniques.Niveau}:gt:50&sort=name");

        Names(inherited).ShouldBe(["Capitaine Orsen", "Ilsa Varenne"]);
        Names(numeric).ShouldBe(["Dragon des Cimes", "Ilsa Varenne"]);
    }

    [Fact]
    public async Task Search_OutOfRules_ReturnsTheBrokenRule()
    {
        // E2E-10-06
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var page = await SearchAsync(backend, root, "outOfRules=true&sort=name");

        Names(page).ShouldBe(["Dragon des Cimes", "Ilsa Varenne"]);
        var issue = page.Hits[1].Issues.Single();
        (issue.PropertyId, issue.PropertyName).ShouldBe((Chroniques.Niveau, "Niveau"));
        issue.Rule["op"]!.GetValue<string>().ShouldBe("less_or_equal");
    }

    [Fact]
    public async Task Search_ModifiedWindow_UsesTheServerClock()
    {
        // E2E-10-07: 24 hours before 13 October 10:00 (Paris) is 12 October 10:00.
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var names = Names(await SearchAsync(backend, root, "kind=page&modified=day"));

        names.ShouldContain("Ilsa Varenne");
        names.ShouldContain("Le Col gelé");
        names.ShouldNotContain("Dragon des Cimes");
    }

    [Fact]
    public async Task Search_Cursor_ChainsThePages()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var all = Names(await SearchAsync(backend, root, "kind=page&sort=name&limit=200"));
        var collected = new List<string>();

        string? cursor = null;
        do
        {
            var page = await SearchAsync(backend, root, "kind=page&sort=name&limit=4" + (cursor is null ? string.Empty : $"&cursor={cursor}"));
            page.Total.ShouldBe(all.Length);
            collected.AddRange(Names(page));
            cursor = page.NextCursor;
        }
        while (cursor is not null);

        collected.ShouldBe(all);
    }

    [Theory]
    [InlineData("where=nonsense")]
    [InlineData("where=44B107E9C2A63F58:gt:beaucoup")]
    [InlineData("where=44B107E9C2A63F58:between:1")]
    [InlineData("sort=oldest")]
    [InlineData("modified=year")]
    [InlineData("kind=nothing")]
    [InlineData("category=xyz")]
    [InlineData("cursor=%25%25")]
    [InlineData("cursor=bm90LWEtY3Vyc29y")]
    public async Task Search_InvalidParameters_ReturnAProblemWithItsCode(string query)
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/search?{query}"), Ct);

        ((int)response.StatusCode).ShouldBeInRange(400, 422);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!;
        problem["code"]!.GetValue<string>().ShouldMatch(@"^(search|request).");
        response.StatusCode.ShouldNotBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Search_ConditionOnAnElementThatIsNotAProperty_IsRefused()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/search?where={Chroniques.Ilsa}:eq:1"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct))!["code"]!.GetValue<string>().ShouldBe(ErrorCodes.PropertyNotFound);
    }

    [Fact]
    public async Task Search_UnknownProject_Returns404()
    {
        var (backend, _) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url("/api/v1/projects/DEADBEEF00000001/search?q=ilsa"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
