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

/// <summary>Categories, properties and page values over HTTP, on the reference project.</summary>
public sealed class StructureApiTests
{
    private static readonly ElementId Marchand = Chroniques.IdOf("Marchand");
    private static readonly ElementId Garde = Chroniques.IdOf("Garde");
    private static readonly ElementId Hostile = Chroniques.IdOf("Hostile");
    private static readonly ElementId Role = Chroniques.IdOf("Rôle");

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

    private static Task<HttpResponseMessage> PutAsync<T>(TestBackend backend, string url, T body) =>
        backend.Client.PutAsJsonAsync(url, body, ApiJson.Options, Ct);

    private static async Task<JsonObject> ProblemAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;

    // ─── Categories ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCategory_Pnj_HasLineageSubCategoriesAndProperties()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var view = await GetAsync<CategoryView>(backend, $"{root}/categories/{Chroniques.Pnj}");

        view.Element.Name.ShouldBe("PNJ");
        view.Description.ShouldStartWith("Personnage non joueur");
        view.Lineage.Select(c => c.Name).ShouldBe(["Entité", "Personnage"]);
        view.SubCategories.Select(c => c.Name).ShouldBe(["Érudit", "Garde", "Marchand"]);
        view.Properties.Select(p => (p.Property.Name, p.Origin)).ShouldBe([("Hostile", ValueOrigin.Own), ("Niveau", ValueOrigin.Own)]);
        view.PageCount.ShouldBe(2);
        view.PageCountWithSubCategories.ShouldBe(2);
    }

    [Fact]
    public async Task GetCategory_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/categories/{Chroniques.Ilsa}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListCategories_ReturnsInheritanceLinks()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var categories = await GetAsync<CategoryListItem[]>(backend, $"{root}/categories");

        categories.Single(c => c.Id == Marchand).ParentCategoryId.ShouldBe(Chroniques.Pnj);
        categories.Single(c => c.Id == Chroniques.Entite).ParentCategoryId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateCategory_WithParent_InheritsDefaults()
    {
        // E2E-07-02
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync($"{root}/categories",
            new CreateCategoryRequest(Roots.Categories.Id, "Forgeron", null, Chroniques.Pnj, "Artisan du métal"));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var view = await response.ReadAsync<CategoryView>();
        var hostile = view.Properties.Single(p => p.Property.Id == Hostile);
        hostile.Origin.ShouldBe(ValueOrigin.Inherited);
        hostile.Source!.Name.ShouldBe("PNJ");
        hostile.Value!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public async Task CreateCategory_UnderPages_Returns422()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync($"{root}/categories", new CreateCategoryRequest(Roots.Pages.Id, "X"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public async Task UpdateCategory_Cycle_Returns422WithTheLoop()
    {
        // E2E-07-03
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PutAsync(backend, $"{root}/categories/{Chroniques.Personnage}", new UpdateCategoryRequest("", Marchand));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await ProblemAsync(response);
        problem["code"]!.GetValue<string>().ShouldBe(ErrorCodes.CategoryCycle);
        problem["loop"]!.AsArray().Select(n => n!.GetValue<string>())
            .ShouldBe(new[] { Chroniques.Personnage, Chroniques.Pnj, Marchand, Chroniques.Personnage }.Select(i => i.ToString()));
    }

    [Fact]
    public async Task UpdateCategory_Description_IsSaved()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PutAsync(backend, $"{root}/categories/{Marchand}", new UpdateCategoryRequest("Vend tout.", Chroniques.Pnj));

        (await response.ReadAsync<CategoryView>()).Description.ShouldBe("Vend tout.");
    }

    [Fact]
    public async Task SetCategoryValue_ThenRemove_UpdatesThePropertyTable()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var set = await PutAsync(backend, $"{root}/categories/{Marchand}/values/{Chroniques.Niveau}", new CategoryValueRequest(JsonValue.Create(15)));
        var view = await set.ReadAsync<CategoryView>();
        var niveau = view.Properties.Single(p => p.Property.Id == Chroniques.Niveau);
        niveau.Origin.ShouldBe(ValueOrigin.Own);
        niveau.Overrides.ShouldBeTrue();

        using var removed = await backend.Client.DeleteAsync(Url($"{root}/categories/{Marchand}/values/{Chroniques.Niveau}"), Ct);
        (await removed.ReadAsync<CategoryView>()).Properties.Single(p => p.Property.Id == Chroniques.Niveau).Origin.ShouldBe(ValueOrigin.Inherited);
    }

    [Fact]
    public async Task SetCategoryValue_WrongType_Returns422()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PutAsync(backend, $"{root}/categories/{Marchand}/values/{Chroniques.Niveau}", new CategoryValueRequest(JsonValue.Create("dix")));

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.PropertyValueInvalid);
    }

    [Fact]
    public async Task GetCategoryPages_WithSubCategories_ListsViaCategory()
    {
        // E2E-07-07
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var direct = await GetAsync<CategoryPageItem[]>(backend, $"{root}/categories/{Garde}/pages");
        var all = await GetAsync<CategoryPageItem[]>(backend, $"{root}/categories/{Chroniques.Pnj}/pages?includeSubCategories=true");

        direct.Select(p => p.Name).ShouldBe(["Capitaine Orsen"]);
        all.Select(p => (p.Name, p.Via.Name)).ShouldBe([("Capitaine Orsen", "Garde"), ("Ilsa Varenne", "Marchand")]);
        all[1].Path.Select(p => p.Name).ShouldBe(["Pages", "Personnages", "PNJ"]);
    }

    // ─── Properties ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProperty_Niveau_ListsUsageAndPagesOutOfRules()
    {
        // E2E-08-01
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var view = await GetAsync<PropertyView>(backend, $"{root}/properties/{Chroniques.Niveau}");

        view.Definition.Type.ShouldBe("int");
        view.Definition.Rules.ToJsonString().ShouldBe("""[{"op":"greater_or_equal","value":1},{"op":"less_or_equal","value":60}]""");
        view.UsedBy.Select(c => c.Name).ShouldBe(["Créature", "Joueur", "PNJ"]);
        view.FilledCount.ShouldBe(3);
        view.Violations.Select(v => (v.PageName, v.Value!.GetValue<long>())).ShouldBe([("Dragon des Cimes", 99L), ("Ilsa Varenne", 72L)]);
        view.Violations[0].Violations.Single().Rule["op"]!.GetValue<string>().ShouldBe("less_or_equal");
    }

    [Fact]
    public async Task ListProperties_ReturnsEveryDefinition()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var definitions = await GetAsync<PropertyDefinitionItem[]>(backend, $"{root}/properties");

        definitions.Select(d => d.Key).ShouldBe(["age", "hostile", "niveau", "points_de_vie", "region", "reputation", "role"], ignoreOrder: true);
        definitions.Single(d => d.Id == Role).Options.Select(o => o.Label).ShouldBe(["Marchande", "Garde", "Érudit", "Voyageur"]);
    }

    [Fact]
    public async Task CreateProperty_Float_IsListed()
    {
        // E2E-08-02
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync($"{root}/properties", new CreatePropertyRequest(
            Roots.Properties.Id, "Poids", "float", Rules: [new JsonObject { ["op"] = "greater_than", ["value"] = 0 }]));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var view = await response.ReadAsync<PropertyView>();
        view.Definition.Key.ShouldBe("poids");
        view.FilledCount.ShouldBe(0);
    }

    [Fact]
    public async Task CreateProperty_UnknownType_Returns422()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync($"{root}/properties", new CreatePropertyRequest(Roots.Properties.Id, "Poids", "decimal"));

        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.PropertyTypeInvalid);
    }

    private static UpdatePropertyRequest Update(PropertyDefinitionItem d, string? key = null, string? type = null, JsonArray? rules = null,
        IReadOnlyList<EnumOptionInput>? options = null, IReadOnlyDictionary<string, string>? replacements = null) =>
        new(key ?? d.Key, d.Description, type ?? d.Type, d.Default?.DeepClone(), rules ?? (JsonArray)d.Rules.DeepClone(),
            options ?? [.. d.Options.Select(o => new EnumOptionInput(o.Key, o.Label))], replacements);

    [Fact]
    public async Task UpdateProperty_Rule60To80_OnePageOutOfRules()
    {
        // E2E-08-05
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var niveau = (await GetAsync<PropertyView>(backend, $"{root}/properties/{Chroniques.Niveau}")).Definition;

        using var response = await PutAsync(backend, $"{root}/properties/{Chroniques.Niveau}", Update(niveau, rules:
            [new JsonObject { ["op"] = "greater_or_equal", ["value"] = 1 }, new JsonObject { ["op"] = "less_or_equal", ["value"] = 80 }]));

        (await response.ReadAsync<PropertyView>()).Violations.Select(v => v.PageName).ShouldBe(["Dragon des Cimes"]);
        var ilsa = await GetAsync<PageView>(backend, $"{root}/pages/{Chroniques.Ilsa}");
        ilsa.Properties.SelectMany(p => p.Violations).ShouldBeEmpty();
    }

    [Fact]
    public async Task UpdateProperty_TakenKey_Returns409()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var niveau = (await GetAsync<PropertyView>(backend, $"{root}/properties/{Chroniques.Niveau}")).Definition;

        using var response = await PutAsync(backend, $"{root}/properties/{Chroniques.Niveau}", Update(niveau, key: "role"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.PropertyKeyTaken);
    }

    [Fact]
    public async Task UpdateProperty_RemoveUsedOption_Returns422WithPages()
    {
        // E2E-08-09
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var role = (await GetAsync<PropertyView>(backend, $"{root}/properties/{Role}")).Definition;

        using var response = await PutAsync(backend, $"{root}/properties/{Role}",
            Update(role, options: [.. role.Options.Where(o => o.Key != "garde").Select(o => new EnumOptionInput(o.Key, o.Label))]));

        var problem = await ProblemAsync(response);
        problem["code"]!.GetValue<string>().ShouldBe(ErrorCodes.PropertyOptionInUse);
        problem["pages"]!.AsArray().Select(p => p!["name"]!.GetValue<string>()).ShouldBe(["Capitaine Orsen"]);
    }

    [Fact]
    public async Task UpdateProperty_IncompatibleType_Returns422WithPages()
    {
        // E2E-08-10
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var role = (await GetAsync<PropertyView>(backend, $"{root}/properties/{Role}")).Definition;

        using var response = await PutAsync(backend, $"{root}/properties/{Role}", Update(role, type: "int", rules: [], options: []));

        var problem = await ProblemAsync(response);
        problem["code"]!.GetValue<string>().ShouldBe(ErrorCodes.PropertyValueNotConvertible);
        problem["pages"]!.AsArray().Select(p => p!["value"]!.GetValue<string>()).ShouldBe(["Garde", "Marchande"]);
    }

    [Fact]
    public async Task UpdateProperty_NewOptionWithoutKey_GetsADerivedKey()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        var role = (await GetAsync<PropertyView>(backend, $"{root}/properties/{Role}")).Definition;

        using var response = await PutAsync(backend, $"{root}/properties/{Role}",
            Update(role, options: [.. role.Options.Select(o => new EnumOptionInput(o.Key, o.Label)), new EnumOptionInput(null, "Chef de guerre")]));

        (await response.ReadAsync<PropertyView>()).Definition.Options[^1].ShouldBe(new EnumOptionItem("chef_de_guerre", "Chef de guerre"));
    }

    [Fact]
    public async Task GetProperty_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/properties/{Chroniques.Pnj}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ─── Pages ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPage_Ilsa_HasCategoriesAndResolvedProperties()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var view = await GetAsync<PageView>(backend, $"{root}/pages/{Chroniques.Ilsa}");

        view.Categories.Select(c => c.Name).ShouldBe(["PNJ", "Marchand"]);
        var hostile = view.Properties.Single(p => p.Property.Id == Hostile);
        hostile.Origin.ShouldBe(ValueOrigin.Inherited);
        hostile.Sources.Select(s => s.Name).ShouldBe(["PNJ"]);
        hostile.OwnValue.ShouldBeNull();
        var niveau = view.Properties.Single(p => p.Property.Id == Chroniques.Niveau);
        niveau.OwnValue!.GetValue<long>().ShouldBe(72);
        niveau.Violations.Single().Rule["value"]!.GetValue<double>().ShouldBe(60);
    }

    [Fact]
    public async Task GetPageProperties_Folder_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await backend.Client.GetAsync(Url($"{root}/pages/{Chroniques.IdOf("Lieux")}/properties"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static async Task<HttpResponseMessage> SaveAsync(TestBackend backend, string root, ElementId page, SavePageContentRequest body)
    {
        var content = await GetAsync<PageContent>(backend, $"{root}/pages/{page}/content");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"{root}/pages/{page}/content")
        {
            Content = JsonContent.Create(body with { Markdown = body.Markdown.Length == 0 ? content.Markdown : body.Markdown }, options: ApiJson.Options),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{content.Revision}\"");
        return await backend.Client.SendAsync(request, Ct);
    }

    [Fact]
    public async Task SaveContent_WithCategoriesAndValues_ResolvesAndKeepsOneRevision()
    {
        // E2E-07-04, E2E-07-05
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await SaveAsync(backend, root, Chroniques.Orsen, new SavePageContentRequest(
            "", [Chroniques.Pnj, Garde, Chroniques.IdOf("Érudit")],
            [new PagePropertyValue(Hostile, JsonValue.Create(true)), new PagePropertyValue(Chroniques.Niveau, JsonValue.Create(40))]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<PageSavedResult>()).Revision.ShouldBe(2);
        var view = await GetAsync<PageView>(backend, $"{root}/pages/{Chroniques.Orsen}");
        view.Categories.Select(c => c.Name).ShouldBe(["PNJ", "Garde", "Érudit"]);
        view.Properties.Single(p => p.Property.Id == Hostile).Origin.ShouldBe(ValueOrigin.Own);
    }

    [Fact]
    public async Task SaveContent_OutOfRuleValue_IsSavedWithAViolation()
    {
        // E2E-05-12: never blocked.
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await SaveAsync(backend, root, Chroniques.Orsen, new SavePageContentRequest(
            "", null, [new PagePropertyValue(Chroniques.Niveau, JsonValue.Create(99))]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var view = await GetAsync<PageView>(backend, $"{root}/pages/{Chroniques.Orsen}");
        view.Properties.Single(p => p.Property.Id == Chroniques.Niveau).Violations.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SaveContent_MalformedValue_Returns422WithTheProperty()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await SaveAsync(backend, root, Chroniques.Orsen, new SavePageContentRequest(
            "", null, [new PagePropertyValue(Chroniques.IdOf("Âge"), JsonValue.Create("vieux"))]));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var problem = await ProblemAsync(response);
        problem["code"]!.GetValue<string>().ShouldBe(ErrorCodes.PropertyValueInvalid);
        problem["propertyId"]!.GetValue<string>().ShouldBe(Chroniques.IdOf("Âge").ToString());
    }

    [Fact]
    public async Task SaveContent_ConflictingCategory_ShowsTheCandidates()
    {
        // E2E-07-06
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        using var created = await backend.Client.PostJsonAsync($"{root}/categories", new CreateCategoryRequest(Roots.Categories.Id, "Montagnard"));
        var montagnard = (await created.ReadAsync<CategoryView>()).Element.Id;
        (await PutAsync(backend, $"{root}/categories/{montagnard}/values/{Hostile}", new CategoryValueRequest(JsonValue.Create(true)))).Dispose();

        (await SaveAsync(backend, root, Chroniques.Ilsa, new SavePageContentRequest("", [Chroniques.Pnj, Marchand, montagnard]))).Dispose();

        var hostile = (await GetAsync<PageProperties>(backend, $"{root}/pages/{Chroniques.Ilsa}/properties")).Properties.Single(p => p.Property.Id == Hostile);
        hostile.Origin.ShouldBe(ValueOrigin.Conflict);
        hostile.Value.ShouldBeNull();
        hostile.Candidates.Select(c => (c.CategoryName, c.Value.GetValue<bool>())).ShouldBe([("PNJ", false), ("Montagnard", true)]);
    }
    // ─── Preview (page editor) ──────────────────────────────────────────────

    private static Task<HttpResponseMessage> PreviewAsync(TestBackend backend, string root, ElementId page, PagePreviewRequest body) =>
        backend.Client.PostAsJsonAsync($"{root}/pages/{page}/properties/preview", body, ApiJson.Options, Ct);

    [Fact]
    public async Task PreviewPageProperties_AddedCategory_ShowsWhatItWouldProvideWithoutSavingAnything()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        using var created = await backend.Client.PostJsonAsync($"{root}/categories", new CreateCategoryRequest(Roots.Categories.Id, "Montagnard"));
        var montagnard = (await created.ReadAsync<CategoryView>()).Element.Id;
        (await PutAsync(backend, $"{root}/categories/{montagnard}/values/{Hostile}", new CategoryValueRequest(JsonValue.Create(true)))).Dispose();

        using var response = await PreviewAsync(backend, root, Chroniques.Ilsa, new PagePreviewRequest([Chroniques.Pnj, Marchand, montagnard], []));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await response.ReadAsync<PageProperties>();
        preview.Categories.Select(c => c.Name).ShouldBe(["PNJ", "Marchand", "Montagnard"]);
        preview.Properties.Single(p => p.Property.Id == Hostile).Origin.ShouldBe(ValueOrigin.Conflict);
        var saved = await GetAsync<PageProperties>(backend, $"{root}/pages/{Chroniques.Ilsa}/properties");
        saved.Categories.Select(c => c.Name).ShouldBe(["PNJ", "Marchand"]);
    }

    [Fact]
    public async Task PreviewPageProperties_OwnValue_ReplacesTheSavedOnesAndIsValidated()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PreviewAsync(backend, root, Chroniques.Ilsa, new PagePreviewRequest(
            [Chroniques.Pnj], [new PagePropertyValue(Chroniques.Niveau, JsonValue.Create(55))]));

        var niveau = (await response.ReadAsync<PageProperties>()).Properties.Single(p => p.Property.Id == Chroniques.Niveau);
        niveau.Value!.GetValue<int>().ShouldBe(55);
        niveau.Violations.ShouldBeEmpty();
        var saved = (await GetAsync<PageProperties>(backend, $"{root}/pages/{Chroniques.Ilsa}/properties")).Properties.Single(p => p.Property.Id == Chroniques.Niveau);
        saved.Value!.GetValue<int>().ShouldBe(72);
    }

    [Fact]
    public async Task PreviewPageProperties_MalformedValue_IsIgnored()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PreviewAsync(backend, root, Chroniques.Ilsa, new PagePreviewRequest(
            [Chroniques.Pnj], [new PagePropertyValue(Chroniques.IdOf("Âge"), JsonValue.Create("vieux"))]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<PageProperties>()).Properties.ShouldNotContain(p => p.Property.Id == Chroniques.IdOf("Âge"));
    }

    [Fact]
    public async Task PreviewPageProperties_Unknown_Returns404()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        using var response = await PreviewAsync(backend, root, ElementId.Parse("DEADBEEF00000001"), new PagePreviewRequest([], []));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
    [Fact]
    public async Task GetPageProperties_OwnValueOverridingACategory_ExposesTheFallback()
    {
        // E2E-07-05: « Rétablir la valeur héritée » needs to know what the page would have without its value.
        var (backend, root) = await StartAsync();
        await using var _ = backend;
        (await SaveAsync(backend, root, Chroniques.Orsen, new SavePageContentRequest(
            "", null, [new PagePropertyValue(Hostile, JsonValue.Create(true))]))).Dispose();

        var hostile = (await GetAsync<PageProperties>(backend, $"{root}/pages/{Chroniques.Orsen}/properties")).Properties.Single(p => p.Property.Id == Hostile);

        hostile.Origin.ShouldBe(ValueOrigin.Own);
        hostile.Fallback.ShouldNotBeNull();
        hostile.Fallback.Origin.ShouldBe(ValueOrigin.Inherited);
        hostile.Fallback.Value!.GetValue<bool>().ShouldBeFalse();
        hostile.Fallback.Sources.Select(c => c.Name).ShouldBe(["PNJ"]);
    }

    [Fact]
    public async Task GetPageProperties_OwnValueOnly_HasNoFallback()
    {
        var (backend, root) = await StartAsync();
        await using var _ = backend;

        var role = (await GetAsync<PageProperties>(backend, $"{root}/pages/{Chroniques.Orsen}/properties")).Properties.Single(p => p.Property.Id == Role);

        role.Origin.ShouldBe(ValueOrigin.Own);
        role.Fallback.ShouldBeNull();
    }
}
