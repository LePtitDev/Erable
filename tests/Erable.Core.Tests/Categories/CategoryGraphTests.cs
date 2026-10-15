using Erable.Core.Categories;

namespace Erable.Core.Tests.Categories;

public sealed class CategoryGraphTests
{
    internal static readonly ElementId Entite = new(0x1001);
    internal static readonly ElementId Personnage = new(0x1002);
    internal static readonly ElementId Pnj = new(0x1003);
    internal static readonly ElementId Marchand = new(0x1004);
    internal static readonly ElementId Garde = new(0x1005);
    internal static readonly ElementId Erudit = new(0x1006);
    internal static readonly ElementId Creature = new(0x1007);
    internal static readonly ElementId Montagnard = new(0x1008);

    /// <summary>Entité › Personnage › PNJ › {Marchand, Garde, Érudit}; Entité › Créature; Montagnard alone.</summary>
    internal static CategoryGraph Chroniques() => CategoryGraph.Load(
    [
        new(Entite, ElementId.None),
        new(Personnage, Entite),
        new(Pnj, Personnage),
        new(Marchand, Pnj),
        new(Garde, Pnj),
        new(Erudit, Pnj),
        new(Creature, Entite),
        new(Montagnard, ElementId.None),
    ]);

    [Fact]
    public void Lineage_Pnj_GoesFromTopMostAncestor()
    {
        Chroniques().Lineage(Pnj).ShouldBe([Entite, Personnage, Pnj]);
    }

    [Fact]
    public void Lineage_UnknownCategory_IsEmpty()
    {
        Chroniques().Lineage(new ElementId(0x9999)).ShouldBeEmpty();
    }

    [Fact]
    public void Ancestors_Marchand_ParentFirst()
    {
        Chroniques().Ancestors(Marchand).ShouldBe([Pnj, Personnage, Entite]);
    }

    [Fact]
    public void Depth_CategoryWithoutParent_IsZero()
    {
        var graph = Chroniques();

        graph.Depth(Entite).ShouldBe(0);
        graph.Depth(Marchand).ShouldBe(3);
    }

    [Fact]
    public void Children_Pnj_AreItsDirectSubCategories()
    {
        Chroniques().Children(Pnj).ShouldBe([Marchand, Garde, Erudit], ignoreOrder: true);
    }

    [Fact]
    public void Descendants_Personnage_IncludesEveryDepth()
    {
        Chroniques().Descendants(Personnage).ShouldBe([Pnj, Marchand, Garde, Erudit], ignoreOrder: true);
    }

    [Fact]
    public void AreRelated_SameChain_IsTrue_OtherChain_IsFalse()
    {
        var graph = Chroniques();

        graph.AreRelated(Marchand, Personnage).ShouldBeTrue();
        graph.AreRelated(Entite, Garde).ShouldBeTrue();
        graph.AreRelated(Pnj, Pnj).ShouldBeTrue();
        graph.AreRelated(Marchand, Garde).ShouldBeFalse();
        graph.AreRelated(Pnj, Montagnard).ShouldBeFalse();
    }

    // F07.R2 ─ cycles

    [Fact]
    public void SetParent_DescendantAsParent_IsRefusedWithTheLoop()
    {
        var graph = Chroniques();

        var error = graph.SetParent(Personnage, Marchand).Error;

        error.Code.ShouldBe(ErrorCodes.CategoryCycle);
        error.Extensions!["loop"].ShouldBe(new[] { Personnage, Pnj, Marchand, Personnage }.Select(i => i.ToString()).ToArray());
        graph.ParentOf(Personnage).ShouldBe(Entite);
    }

    [Fact]
    public void SetParent_Itself_IsRefusedWithATwoStepLoop()
    {
        var error = Chroniques().SetParent(Pnj, Pnj).Error;

        error.Code.ShouldBe(ErrorCodes.CategoryCycle);
        error.Extensions!["loop"].ShouldBe(new[] { Pnj.ToString(), Pnj.ToString() });
    }

    [Fact]
    public void SetParent_DirectChildAsParent_LoopHasThreeSteps()
    {
        var error = Chroniques().SetParent(Pnj, Garde).Error;

        error.Extensions!["loop"].ShouldBe(new[] { Pnj, Garde, Pnj }.Select(i => i.ToString()).ToArray());
    }

    [Fact]
    public void SetParent_UnrelatedCategory_Reparents()
    {
        var graph = Chroniques();

        graph.SetParent(Montagnard, Personnage).IsSuccess.ShouldBeTrue();

        graph.Lineage(Montagnard).ShouldBe([Entite, Personnage, Montagnard]);
        graph.Children(Personnage).ShouldContain(Montagnard);
    }

    [Fact]
    public void SetParent_None_DetachesFromParent()
    {
        var graph = Chroniques();

        graph.SetParent(Pnj, ElementId.None).IsSuccess.ShouldBeTrue();

        graph.Lineage(Marchand).ShouldBe([Pnj, Marchand]);
        graph.Children(Personnage).ShouldBeEmpty();
    }

    [Fact]
    public void SetParent_UnknownParent_IsRefused()
    {
        Chroniques().SetParent(Pnj, new ElementId(0x9999)).Error.Code.ShouldBe(ErrorCodes.CategoryParentNotFound);
    }

    [Fact]
    public void SetParent_UnknownCategory_IsRefused()
    {
        Chroniques().SetParent(new ElementId(0x9999), Pnj).Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
    }

    [Fact]
    public void Add_WithParent_InheritsTheChain()
    {
        var graph = Chroniques();
        var forgeron = new ElementId(0x2000);

        graph.Add(forgeron, Pnj).IsSuccess.ShouldBeTrue();

        graph.Lineage(forgeron).ShouldBe([Entite, Personnage, Pnj, forgeron]);
    }

    [Fact]
    public void Add_ExistingId_IsRefused()
    {
        Chroniques().Add(Pnj, ElementId.None).Error.Code.ShouldBe(ErrorCodes.IdAlreadyUsed);
    }

    [Fact]
    public void Add_UnknownParent_IsRefused()
    {
        Chroniques().Add(new ElementId(0x2000), new ElementId(0x9999)).Error.Code.ShouldBe(ErrorCodes.CategoryParentNotFound);
    }

    [Fact]
    public void Remove_Parent_SubCategoriesLoseItUntilRestored()
    {
        var graph = Chroniques();

        graph.Remove(Pnj).ShouldBeTrue();
        graph.ParentOf(Marchand).ShouldBe(ElementId.None);
        graph.Children(Personnage).ShouldBeEmpty();

        graph.Add(Pnj, Personnage).IsSuccess.ShouldBeTrue();
        graph.Lineage(Marchand).ShouldBe([Entite, Personnage, Pnj, Marchand]);
    }

    [Fact]
    public void Remove_Unknown_ReturnsFalse()
    {
        Chroniques().Remove(new ElementId(0x9999)).ShouldBeFalse();
    }

    [Fact]
    public void Load_UnknownParent_IsIgnoredByTraversals()
    {
        var graph = CategoryGraph.Load([new(Pnj, new ElementId(0x9999))]);

        graph.ParentOf(Pnj).ShouldBe(ElementId.None);
        graph.Lineage(Pnj).ShouldBe([Pnj]);
    }

    [Fact]
    public void FindCycles_CorruptedData_ReportsEveryCategoryOfTheLoop()
    {
        var graph = CategoryGraph.Load([new(Pnj, Marchand), new(Marchand, Pnj), new(Garde, Pnj)]);

        graph.FindCycles().ShouldBe([Pnj, Marchand], ignoreOrder: true);
        graph.Ancestors(Garde).ShouldBe([Pnj, Marchand]);
        graph.Count.ShouldBe(3);
    }

    [Fact]
    public void FindCycles_HealthyGraph_IsEmpty()
    {
        Chroniques().FindCycles().ShouldBeEmpty();
    }

    [Fact]
    public void Nodes_RoundTripThroughLoad()
    {
        var graph = Chroniques();

        CategoryGraph.Load(graph.Nodes).Nodes.ShouldBe(graph.Nodes, ignoreOrder: true);
    }
}
