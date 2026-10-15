using System.Diagnostics.CodeAnalysis;
using Erable.Core.Categories;
using static Erable.Core.Tests.Categories.CategoryGraphTests;

namespace Erable.Core.Tests.Categories;

/// <summary>Examples of the mockup (docs/testing.md §4.2): inheritance, override, conflict.</summary>
public sealed class ValueResolverTests
{
    private static readonly ElementId Ilsa = new(0x3001);

    private static ValueLookup<string> Values(params (ElementId Owner, string Value)[] values)
    {
        var map = values.ToDictionary(v => v.Owner, v => v.Value);
        return (ElementId owner, [MaybeNullWhen(false)] out string value) => map.TryGetValue(owner, out value);
    }

    private static Resolution<string> ForIlsa(ValueLookup<string> values, string? definitionDefault = null, params ElementId[] categories) =>
        ValueResolver.ForPage(Chroniques(), Ilsa, categories, values, definitionDefault);

    [Fact]
    public void ForPage_OwnValue_WinsOverEverything()
    {
        var result = ForIlsa(Values((Ilsa, "Oui"), (Pnj, "Non")), "Non", Pnj);

        result.ShouldBe(new Resolution<string>.Own("Oui"));
        result.EffectiveValue.ShouldBe("Oui");
    }

    [Fact]
    public void ForPage_PnjInheritsFromEntite_SourceIsTheProvider()
    {
        var result = ForIlsa(Values((Entite, "Non")), null, Pnj).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("Non");
        result.Source.ShouldBe(Entite);
    }

    [Fact]
    public void ForPage_MostSpecificCategoryOfTheChain_Wins()
    {
        var result = ForIlsa(Values((Entite, "1"), (Personnage, "2"), (Pnj, "10")), null, Pnj).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("10");
        result.Source.ShouldBe(Pnj);
    }

    [Fact]
    public void ForPage_PnjAndMarchand_DeepestWinsWithoutConflict()
    {
        var result = ForIlsa(Values((Pnj, "10"), (Marchand, "20")), null, Pnj, Marchand).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("20");
        result.Sources.ShouldBe([Marchand]);
    }

    [Fact]
    public void ForPage_PnjAndMarchandWithoutOwnValue_InheritsPnjOnce()
    {
        var result = ForIlsa(Values((Pnj, "Non")), null, Marchand, Pnj).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Sources.ShouldBe([Pnj]);
    }

    [Fact]
    public void ForPage_SubCategoryValueOverridesValueInheritedBySibling()
    {
        // Garde defines 5; Marchand inherits 10 from PNJ, an ancestor of Garde: Garde is more specific.
        var result = ForIlsa(Values((Pnj, "10"), (Garde, "5")), null, Marchand, Garde).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("5");
    }

    [Fact]
    public void ForPage_UnrelatedCategoriesWithDifferentValues_AreInConflict()
    {
        // E2E-07-06: PNJ (Non) / Montagnard (Oui).
        var result = ForIlsa(Values((Pnj, "Non"), (Montagnard, "Oui")), "Non", Pnj, Marchand, Montagnard)
            .ShouldBeOfType<Resolution<string>.Conflict>();

        result.Candidates.ShouldBe([new ValueCandidate<string>(Pnj, "Non"), new ValueCandidate<string>(Montagnard, "Oui")]);
        result.EffectiveValue.ShouldBeNull();
    }

    [Fact]
    public void ForPage_ConflictFixedOnThePage_IsOwn()
    {
        ForIlsa(Values((Pnj, "Non"), (Montagnard, "Oui"), (Ilsa, "Non")), null, Pnj, Montagnard)
            .ShouldBe(new Resolution<string>.Own("Non"));
    }

    [Fact]
    public void ForPage_UnrelatedCategoriesAgreeing_AreNotInConflict()
    {
        var result = ForIlsa(Values((Pnj, "Non"), (Montagnard, "Non")), null, Montagnard, Pnj).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("Non");
        // Most specific first: PNJ (depth 2) before Montagnard (depth 0).
        result.Sources.ShouldBe([Pnj, Montagnard]);
    }

    [Fact]
    public void ForPage_NoProvider_FallsBackToDefinitionDefault()
    {
        ForIlsa(Values(), "1", Pnj).ShouldBe(new Resolution<string>.FromDefinition("1"));
    }

    [Fact]
    public void ForPage_NothingAnywhere_IsUnset()
    {
        var result = ForIlsa(Values(), null, Pnj);

        result.ShouldBe(new Resolution<string>.Unset());
        result.EffectiveValue.ShouldBeNull();
    }

    [Fact]
    public void ForPage_NoCategory_UsesDefinitionDefault()
    {
        ForIlsa(Values((Pnj, "10")), "1").ShouldBe(new Resolution<string>.FromDefinition("1"));
    }

    [Fact]
    public void ForPage_DeletedCategory_IsIgnored()
    {
        ForIlsa(Values((new ElementId(0x9999), "x")), null, new ElementId(0x9999)).ShouldBe(new Resolution<string>.Unset());
    }

    [Fact]
    public void ForPage_CustomComparer_DecidesAgreement()
    {
        var result = ValueResolver.ForPage(Chroniques(), Ilsa, [Pnj, Montagnard], Values((Pnj, "non"), (Montagnard, "NON")), null,
            StringComparer.OrdinalIgnoreCase);

        result.ShouldBeOfType<Resolution<string>.Inherited>();
    }

    // Categories (E2E-07-02, E2E-07-08)

    [Fact]
    public void ForCategory_NewChildOfPnj_InheritsPnjDefault()
    {
        var graph = Chroniques();
        var forgeron = new ElementId(0x2000);
        graph.Add(forgeron, Pnj);

        var result = ValueResolver.ForCategory(graph, forgeron, Values((Pnj, "Non")), null).ShouldBeOfType<Resolution<string>.Inherited>();

        result.Source.ShouldBe(Pnj);
    }

    [Fact]
    public void ForCategory_OwnValue_IsOwn()
    {
        ValueResolver.ForCategory(Chroniques(), Pnj, Values((Pnj, "10"), (Entite, "1")), null).ShouldBe(new Resolution<string>.Own("10"));
    }

    [Fact]
    public void ForCategory_NearestAncestor_Wins()
    {
        var result = ValueResolver.ForCategory(Chroniques(), Marchand, Values((Personnage, "2"), (Entite, "1")), null)
            .ShouldBeOfType<Resolution<string>.Inherited>();

        result.Value.ShouldBe("2");
    }

    [Fact]
    public void ForCategory_NoValue_UsesDefinitionDefaultOrUnset()
    {
        ValueResolver.ForCategory(Chroniques(), Pnj, Values(), "1").ShouldBe(new Resolution<string>.FromDefinition("1"));
        ValueResolver.ForCategory(Chroniques(), Pnj, Values(), null).ShouldBe(new Resolution<string>.Unset());
    }

    // F07.R4 ─ pages of a category

    private static readonly ElementId Orsen = new(0x3002);
    private static readonly ElementId Dragon = new(0x3003);

    private static (ElementId, IReadOnlyList<ElementId>)[] Pages() =>
    [
        (Ilsa, [Pnj, Marchand]),
        (Orsen, [Pnj, Garde]),
        (Dragon, [Creature]),
    ];

    [Fact]
    public void PagesOf_WithoutSubCategories_ListsDirectMembers()
    {
        CategoryMembership.PagesOf(Chroniques(), Marchand, false, Pages()).ShouldBe([new CategoryMember(Ilsa, Marchand)]);
    }

    [Fact]
    public void PagesOf_WithSubCategories_ViaTheDeepestCategory()
    {
        CategoryMembership.PagesOf(Chroniques(), Pnj, true, Pages())
            .ShouldBe([new CategoryMember(Ilsa, Marchand), new CategoryMember(Orsen, Garde)]);
    }

    [Fact]
    public void PagesOf_Entite_WithSubCategories_IncludesEveryBranch()
    {
        CategoryMembership.PagesOf(Chroniques(), Entite, true, Pages()).Select(m => m.PageId).ShouldBe([Ilsa, Orsen, Dragon]);
    }

    [Fact]
    public void PagesOf_Entite_WithoutSubCategories_IsEmpty()
    {
        CategoryMembership.PagesOf(Chroniques(), Entite, false, Pages()).ShouldBeEmpty();
    }
}
