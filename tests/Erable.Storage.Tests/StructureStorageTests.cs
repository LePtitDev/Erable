using System.Diagnostics;
using System.Text.Json;
using Dapper;
using Erable.Core;
using Erable.Core.Categories;
using Erable.Core.Elements;
using Erable.Core.Properties;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>Categories, definitions, values and derived tables on the reference project (M2-03).</summary>
public sealed class StructureStorageTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;

    private static readonly ElementId Marchand = Chroniques.IdOf("Marchand");
    private static readonly ElementId Garde = Chroniques.IdOf("Garde");
    private static readonly ElementId Hostile = Chroniques.IdOf("Hostile");
    private static readonly ElementId Role = Chroniques.IdOf("Rôle");
    private static readonly ElementId Age = Chroniques.IdOf("Âge");

    public StructureStorageTests()
    {
        _project = Chroniques.Write(_dir.Path);
        _service = new ProjectService(_project, new FakeTimeProvider(Chroniques.Now));
    }

    public void Dispose()
    {
        _project.Dispose();
        _dir.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static JsonElement Json(string json) => JsonSerializer.Deserialize<JsonElement>(json);

    private ResolvedProperty Property(ElementId page, ElementId property) =>
        _service.LoadStructure([page]).ResolvePage(page).Single(p => p.Definition.Id == property);

    private long Revision(ElementId id) => _project.Repository.GetElement(id)!.Revision;

    private IReadOnlyList<(long Page, string Op)> Issues(ElementId property)
    {
        using var connection = _project.Database.OpenConnection();
        return [.. connection.Query<(long, string)>(
            "SELECT owner_id, rule_op FROM validation_issues WHERE property_id = @Id ORDER BY owner_id", new { Id = property.ToInt64() })];
    }

    private static PropertyInput Input(PropertyDefinition d, string? type = null, string? rules = null, IReadOnlyList<(string?, string)>? options = null,
        IReadOnlyDictionary<string, string>? replacements = null, string? key = null) =>
        new(key ?? d.Key, d.Description, type ?? d.Type.ToString(), d.Default is null ? null : Json(PropertyValues.ToJsonString(d.Default)),
            Json(rules ?? ValidationRules.ListToJson(d.Rules).ToJsonString()),
            options ?? [.. d.Options.Select(o => ((string?)o.Key, o.Label))], replacements);

    private PropertyDefinition Definition(ElementId id) => _service.ListDefinitions().Single(d => d.Id == id);

    // ─── Reference project ──────────────────────────────────────────────────

    [Fact]
    public void ResolvePage_Ilsa_HasOwnInheritedAndOutOfRuleValues()
    {
        var state = _service.LoadStructure([Chroniques.Ilsa]);

        state.CategoriesOf(Chroniques.Ilsa).ShouldBe([Chroniques.Pnj, Marchand]);
        var niveau = Property(Chroniques.Ilsa, Chroniques.Niveau);
        niveau.Origin.ShouldBe(ValueOrigin.Own);
        niveau.Resolution.EffectiveValue.ShouldBe(new IntValue(72));
        niveau.Violations.Single().Rule.ShouldBe(new NumberRule(RuleOp.LessOrEqual, 60));
        var hostile = Property(Chroniques.Ilsa, Hostile);
        hostile.Origin.ShouldBe(ValueOrigin.Inherited);
        hostile.Resolution.ShouldBeOfType<Resolution<PropertyValue>.Inherited>().Source.ShouldBe(Chroniques.Pnj);
        hostile.Resolution.EffectiveValue.ShouldBe(new BoolValue(false));
    }

    [Fact]
    public void DerivedTables_ReferenceProject_ListTwoPagesOutOfRules()
    {
        Issues(Chroniques.Niveau).Select(i => ElementId.FromInt64(i.Page)).ShouldBe([Chroniques.Ilsa, Chroniques.Dragon], ignoreOrder: true);
    }

    [Fact]
    public void ResolvePage_RegionInCategoryRule_IsSatisfiedByHautbois()
    {
        Property(Chroniques.Ilsa, Chroniques.IdOf("Propriété Région")).Violations.ShouldBeEmpty();
    }

    // ─── Categories ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_WithParent_InheritsItsDefaults()
    {
        var created = await _service.CreateCategoryAsync(Roots.Categories.Id, "Forgeron", null, new CategoryInput("Artisan du métal", Chroniques.Pnj), "local", Ct);

        var state = _service.LoadStructure([]);
        state.Graph.Lineage(created.Value.Id).ShouldBe([Chroniques.Entite, Chroniques.Personnage, Chroniques.Pnj, created.Value.Id]);
        state.Categories[created.Value.Id].Description.ShouldBe("Artisan du métal");
        var hostile = state.ResolveCategory(created.Value.Id, state.Definitions[Hostile]);
        hostile.ShouldBeOfType<Resolution<PropertyValue>.Inherited>().Source.ShouldBe(Chroniques.Pnj);
    }

    [Fact]
    public async Task CreateCategory_UnknownParent_IsRefused()
    {
        var created = await _service.CreateCategoryAsync(Roots.Categories.Id, "X", null, new CategoryInput("", new ElementId(0x99999)), "local", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.CategoryParentNotFound);
    }

    [Fact]
    public async Task UpdateCategory_Cycle_IsRefusedAndChangesNothing()
    {
        var revision = Revision(Chroniques.Personnage);

        var updated = await _service.UpdateCategoryAsync(Chroniques.Personnage, new CategoryInput("Être doué de parole.", Marchand), "local", Ct);

        updated.Error.Code.ShouldBe(ErrorCodes.CategoryCycle);
        updated.Error.Extensions!["loop"].ShouldBe(new[] { Chroniques.Personnage, Chroniques.Pnj, Marchand, Chroniques.Personnage }.Select(i => i.ToString()).ToArray());
        Revision(Chroniques.Personnage).ShouldBe(revision);
    }

    [Fact]
    public async Task UpdateCategory_Description_NewRevisionWithSnapshot()
    {
        var before = Revision(Chroniques.Pnj);

        (await _service.UpdateCategoryAsync(Chroniques.Pnj, new CategoryInput("Nouvelle description", Chroniques.Personnage), "local", Ct)).IsSuccess.ShouldBeTrue();

        Revision(Chroniques.Pnj).ShouldBe(before + 1);
        using var connection = _project.Database.OpenConnection();
        var snapshot = RevisionSnapshot.Deserialize(connection.ExecuteScalar<byte[]>(
            "SELECT snapshot FROM revisions WHERE element_id = @Id ORDER BY revision DESC LIMIT 1", new { Id = Chroniques.Pnj.ToInt64() })!);
        snapshot.Category!.Description.ShouldBe("Nouvelle description");
        snapshot.Values!.Keys.ShouldContain(Hostile.ToString());
    }

    [Fact]
    public async Task SetCategoryValue_PnjNiveauTo15_PropagatesToPagesWithoutOwnValue()
    {
        // E2E-07-08: a PNJ page without its own Niveau shows 15; Ilsa keeps 72.
        var page = await _service.CreateElementAsync(ElementKind.Page, Roots.Pages.Id, "Garde anonyme", null, "local", cancellationToken: Ct);
        await _service.SavePageContentAsync(page.Value.Id, "", 1, "local", new PageStructureInput([Garde], null), Ct);

        (await _service.SetCategoryValueAsync(Chroniques.Pnj, Chroniques.Niveau, Json("15"), "local", Ct)).IsSuccess.ShouldBeTrue();

        Property(page.Value.Id, Chroniques.Niveau).Resolution.EffectiveValue.ShouldBe(new IntValue(15));
        Property(Chroniques.Ilsa, Chroniques.Niveau).Resolution.EffectiveValue.ShouldBe(new IntValue(72));
        using var connection = _project.Database.OpenConnection();
        connection.ExecuteScalar<long>("SELECT v_int FROM resolved_values WHERE page_id = @Page AND property_id = @Property",
            new { Page = page.Value.Id.ToInt64(), Property = Chroniques.Niveau.ToInt64() }).ShouldBe(15);
    }

    [Fact]
    public async Task SetCategoryValue_InvalidValue_IsRefused()
    {
        (await _service.SetCategoryValueAsync(Chroniques.Pnj, Chroniques.Niveau, Json("\"dix\""), "local", Ct)).Error.Code
            .ShouldBe(ErrorCodes.PropertyValueInvalid);
    }

    [Fact]
    public async Task SetCategoryValue_Null_RemovesTheDefault()
    {
        await _service.SetCategoryValueAsync(Chroniques.Pnj, Hostile, null, "local", Ct);

        _service.LoadStructure([Chroniques.Orsen]).ResolvePage(Chroniques.Orsen).ShouldNotContain(p => p.Definition.Id == Hostile);
    }

    [Fact]
    public async Task ConflictingCategories_PageMustFixTheValue()
    {
        // E2E-07-06
        var montagnard = (await _service.CreateCategoryAsync(Roots.Categories.Id, "Montagnard", null, new CategoryInput("", ElementId.None), "local", Ct)).Value.Id;
        await _service.SetCategoryValueAsync(montagnard, Hostile, Json("true"), "local", Ct);

        var markdown = _project.Repository.GetPageContent(Chroniques.Ilsa)!.Markdown;
        await _service.SavePageContentAsync(Chroniques.Ilsa, markdown, Revision(Chroniques.Ilsa), "local",
            new PageStructureInput([Chroniques.Pnj, Marchand, montagnard], null), Ct);

        var conflict = Property(Chroniques.Ilsa, Hostile).Resolution.ShouldBeOfType<Resolution<PropertyValue>.Conflict>();
        conflict.Candidates.Select(c => c.CategoryId).ShouldBe([Chroniques.Pnj, montagnard]);
    }

    [Fact]
    public void GetCategoryPages_PnjWithSubCategories_ViaTheMostSpecific()
    {
        _service.GetCategoryPages(Chroniques.Pnj, true).ShouldBe(
            [new CategoryMember(Chroniques.Ilsa, Marchand), new CategoryMember(Chroniques.Orsen, Garde)], ignoreOrder: true);
    }

    // ─── Page values ────────────────────────────────────────────────────────

    [Fact]
    public async Task SavePage_ValuesAndCategories_OneRevisionAndDerivedRows()
    {
        var revision = Revision(Chroniques.Orsen);
        var markdown = _project.Repository.GetPageContent(Chroniques.Orsen)!.Markdown;

        var saved = await _service.SavePageContentAsync(Chroniques.Orsen, markdown, revision, "local",
            new PageStructureInput([Chroniques.Pnj, Garde, Chroniques.IdOf("Érudit")], [(Hostile, Json("true")), (Chroniques.Niveau, Json("40"))]), Ct);

        saved.Value.Revision.ShouldBe(revision + 1);
        _project.Repository.CountRevisions(Chroniques.Orsen).ShouldBe(2);
        Property(Chroniques.Orsen, Hostile).Origin.ShouldBe(ValueOrigin.Own);
        // Rôle was not sent: it is no longer defined by the page.
        _service.LoadStructure([Chroniques.Orsen]).ValueOf(Chroniques.Orsen, Role).ShouldBeNull();
        _service.LoadStructure([Chroniques.Orsen]).CategoriesOf(Chroniques.Orsen).Count.ShouldBe(3);
    }

    [Fact]
    public async Task SavePage_OutOfRuleValue_IsSavedWithAnIssue()
    {
        var markdown = _project.Repository.GetPageContent(Chroniques.Orsen)!.Markdown;

        var saved = await _service.SavePageContentAsync(Chroniques.Orsen, markdown, Revision(Chroniques.Orsen), "local",
            new PageStructureInput(null, [(Chroniques.Niveau, Json("90"))]), Ct);

        saved.IsSuccess.ShouldBeTrue();
        Issues(Chroniques.Niveau).Select(i => ElementId.FromInt64(i.Page)).ShouldContain(Chroniques.Orsen);
    }

    [Fact]
    public async Task SavePage_MalformedValue_IsRefusedAndChangesNothing()
    {
        var revision = Revision(Chroniques.Orsen);

        var saved = await _service.SavePageContentAsync(Chroniques.Orsen, "x", revision, "local", new PageStructureInput(null, [(Age, Json("\"vieux\""))]), Ct);

        saved.Error.Code.ShouldBe(ErrorCodes.PropertyValueInvalid);
        saved.Error.Extensions!["propertyId"].ShouldBe(Age.ToString());
        Revision(Chroniques.Orsen).ShouldBe(revision);
    }

    [Fact]
    public async Task SavePage_UnknownCategory_IsRefused()
    {
        var saved = await _service.SavePageContentAsync(Chroniques.Orsen, "x", Revision(Chroniques.Orsen), "local",
            new PageStructureInput([new ElementId(0x99999)], null), Ct);

        saved.Error.Code.ShouldBe(ErrorCodes.PageCategoryInvalid);
    }

    [Fact]
    public async Task SavePage_UnknownProperty_IsRefused()
    {
        var saved = await _service.SavePageContentAsync(Chroniques.Orsen, "x", Revision(Chroniques.Orsen), "local",
            new PageStructureInput(null, [(new ElementId(0x99999), Json("1"))]), Ct);

        saved.Error.Code.ShouldBe(ErrorCodes.PropertyNotFound);
    }

    [Fact]
    public async Task SavePage_WithRecompute_StaysUnderTheBudget()
    {
        var markdown = _project.Repository.GetPageContent(Chroniques.Ilsa)!.Markdown;
        await _service.SavePageContentAsync(Chroniques.Ilsa, markdown, Revision(Chroniques.Ilsa), "local", new PageStructureInput(null, null), Ct);

        var stopwatch = Stopwatch.StartNew();
        await _service.SavePageContentAsync(Chroniques.Ilsa, markdown + "\nPlus.", Revision(Chroniques.Ilsa), "local",
            new PageStructureInput([Chroniques.Pnj, Marchand], [(Chroniques.Niveau, Json("55"))]), Ct);

        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromMilliseconds(100));
    }

    // ─── Properties ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProperty_Float_KeyDerivedFromName()
    {
        var created = await _service.CreatePropertyAsync(Roots.Properties.Id, "Poids", null,
            new PropertyInput(null, "", "float", null, Json("""[{"op":"greater_than","value":0}]"""), []), "local", Ct);

        var definition = Definition(created.Value.Id);
        definition.Key.ShouldBe("poids");
        definition.Type.ShouldBe(PropertyType.Float);
        definition.Rules.ShouldBe([new NumberRule(RuleOp.GreaterThan, 0)]);
    }

    [Fact]
    public async Task CreateProperty_RuleNotAvailable_IsRefused()
    {
        var created = await _service.CreatePropertyAsync(Roots.Properties.Id, "Actif", null,
            new PropertyInput(null, "", "bool", null, Json("""[{"op":"not_equal","value":1}]"""), []), "local", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.PropertyRuleInvalid);
    }

    [Fact]
    public async Task CreateProperty_TakenKey_IsRefused()
    {
        var created = await _service.CreatePropertyAsync(Roots.Properties.Id, "Niveau bis", null,
            new PropertyInput("niveau", "", "int", null, null, []), "local", Ct);

        created.Error.Code.ShouldBe(ErrorCodes.PropertyKeyTaken);
    }

    [Fact]
    public async Task UpdateProperty_Rule60To80_RecomputesEveryPageInTheSameTransaction()
    {
        // E2E-08-05
        var niveau = Definition(Chroniques.Niveau);

        var updated = await _service.UpdatePropertyAsync(Chroniques.Niveau,
            Input(niveau, rules: """[{"op":"greater_or_equal","value":1},{"op":"less_or_equal","value":80}]"""), "local", Ct);

        updated.IsSuccess.ShouldBeTrue();
        Issues(Chroniques.Niveau).Select(i => ElementId.FromInt64(i.Page)).ShouldBe([Chroniques.Dragon]);
    }

    [Fact]
    public async Task UpdateProperty_RenameKey_ValuesUntouched()
    {
        // E2E-08-11
        var niveau = Definition(Chroniques.Niveau);

        (await _service.UpdatePropertyAsync(Chroniques.Niveau, Input(niveau, key: "level"), "local", Ct)).IsSuccess.ShouldBeTrue();

        Definition(Chroniques.Niveau).Key.ShouldBe("level");
        Property(Chroniques.Ilsa, Chroniques.Niveau).Resolution.EffectiveValue.ShouldBe(new IntValue(72));
    }

    [Fact]
    public async Task UpdateProperty_RenameOptionLabel_PagesShowTheNewLabel()
    {
        // E2E-08-08: the value stores the key; only the label changes.
        var role = Definition(Role);
        var options = role.Options.Select(o => ((string?)o.Key, o.Key == "marchande" ? "Négociante" : o.Label)).ToArray();

        (await _service.UpdatePropertyAsync(Role, Input(role, options: options), "local", Ct)).IsSuccess.ShouldBeTrue();

        Property(Chroniques.Ilsa, Role).Resolution.EffectiveValue.ShouldBe(new EnumValue("marchande"));
        Definition(Role).Options.Single(o => o.Key == "marchande").Label.ShouldBe("Négociante");
    }

    [Fact]
    public async Task UpdateProperty_RemoveUsedOptionWithoutReplacement_ListsThePages()
    {
        // E2E-08-09
        var role = Definition(Role);
        var options = role.Options.Where(o => o.Key != "garde").Select(o => ((string?)o.Key, o.Label)).ToArray();

        var updated = await _service.UpdatePropertyAsync(Role, Input(role, options: options), "local", Ct);

        updated.Error.Code.ShouldBe(ErrorCodes.PropertyOptionInUse);
        var pages = (Dictionary<string, object?>[])updated.Error.Extensions!["pages"]!;
        pages.Select(p => p["name"]).ShouldBe(["Capitaine Orsen"]);
    }

    [Fact]
    public async Task UpdateProperty_RemoveUsedOptionWithReplacement_RewritesTheValues()
    {
        var role = Definition(Role);
        var options = role.Options.Where(o => o.Key != "garde").Select(o => ((string?)o.Key, o.Label)).ToArray();

        var updated = await _service.UpdatePropertyAsync(Role, Input(role, options: options, replacements: new Dictionary<string, string> { ["garde"] = "voyageur" }), "local", Ct);

        updated.IsSuccess.ShouldBeTrue();
        Property(Chroniques.Orsen, Role).Resolution.EffectiveValue.ShouldBe(new EnumValue("voyageur"));
    }

    [Fact]
    public async Task UpdateProperty_IncompatibleTypeChange_ListsTheBlockingPages()
    {
        // E2E-08-10
        var role = Definition(Role);

        var updated = await _service.UpdatePropertyAsync(Role, Input(role, type: "int", rules: "[]", options: []), "local", Ct);

        updated.Error.Code.ShouldBe(ErrorCodes.PropertyValueNotConvertible);
        var pages = (Dictionary<string, object?>[])updated.Error.Extensions!["pages"]!;
        pages.Select(p => p["name"]).ShouldBe(["Capitaine Orsen", "Ilsa Varenne"]);
        Definition(Role).Type.ShouldBe(PropertyType.Enum);
    }

    [Fact]
    public async Task UpdateProperty_CompatibleTypeChange_ConvertsTheValues()
    {
        var niveau = Definition(Chroniques.Niveau);

        (await _service.UpdatePropertyAsync(Chroniques.Niveau, Input(niveau, type: "float"), "local", Ct)).IsSuccess.ShouldBeTrue();

        Property(Chroniques.Ilsa, Chroniques.Niveau).Resolution.EffectiveValue.ShouldBe(new FloatValue(72));
        _service.LoadStructure([]).ValueOf(Chroniques.Pnj, Chroniques.Niveau).ShouldBe(new FloatValue(10));
    }

    [Fact]
    public async Task UpdateProperty_Default_DoesNotAddThePropertyToPagesThatDoNotHaveIt()
    {
        var age = Definition(Age);

        await _service.UpdatePropertyAsync(Age, new PropertyInput(age.Key, "", "int", Json("30"), Json("[]"), []), "local", Ct);

        _service.LoadStructure([Chroniques.Hautbois]).ResolvePage(Chroniques.Hautbois).ShouldNotContain(p => p.Definition.Id == Age);
    }

    // ─── Doctor ─────────────────────────────────────────────────────────────

    [Fact]
    public void Doctor_ReferenceProject_StructureIsHealthy()
    {
        ProjectDoctor.Diagnose(_project, fix: false).Healthy.ShouldBeTrue();
    }

    [Fact]
    public void Doctor_DerivedRowsTampered_ReportsAndRebuilds()
    {
        _project.Database.Write((c, t) => c.Execute("DELETE FROM validation_issues", transaction: t));

        ProjectDoctor.Diagnose(_project, fix: true).Findings.Single(f => f.Code == DoctorCodes.DerivedOutOfSync).Fixed.ShouldBeTrue();

        Issues(Chroniques.Niveau).Count.ShouldBe(2);
        ProjectDoctor.Diagnose(_project, fix: false).Healthy.ShouldBeTrue();
    }

    [Fact]
    public void Doctor_CategoryCycle_IsAnError()
    {
        _project.Database.Write((c, t) => c.Execute("UPDATE categories SET parent_category_id = @Marchand WHERE id = @Personnage",
            new { Marchand = Marchand.ToInt64(), Personnage = Chroniques.Personnage.ToInt64() }, t));

        ProjectDoctor.Diagnose(_project, fix: false).Findings.Where(f => f.Code == DoctorCodes.CategoryCycle).Select(f => f.ElementId)
            .ShouldBe([Chroniques.Personnage, Chroniques.Pnj, Marchand], ignoreOrder: true);
    }

    [Fact]
    public void Doctor_ValueOfTheWrongType_IsAnError()
    {
        _project.Database.Write((c, t) => c.Execute("UPDATE property_values SET v_int = NULL, v_text = 'x' WHERE owner_id = @Ilsa AND property_id = @Niveau",
            new { Ilsa = Chroniques.Ilsa.ToInt64(), Niveau = Chroniques.Niveau.ToInt64() }, t));

        ProjectDoctor.Diagnose(_project, fix: false).Findings.ShouldContain(f => f.Code == DoctorCodes.ValueInvalid && f.ElementId == Chroniques.Ilsa);
    }

    [Fact]
    public void Doctor_CategoryRowMissing_IsAnError()
    {
        _project.Database.Write((c, t) => c.Execute("DELETE FROM categories WHERE id = @Id", new { Id = Garde.ToInt64() }, t));

        ProjectDoctor.Diagnose(_project, fix: false).Findings.ShouldContain(f => f.Code == DoctorCodes.StructureRowMissing && f.ElementId == Garde);
    }
}
