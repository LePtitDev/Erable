using Erable.Core.Elements;
using Erable.Core.Tree;
using Erable.Testing;

namespace Erable.Core.Tests.Tree;

public sealed class ElementTreeTests
{
    private static ProjectBuilder Chroniques() => Project.Build()
        .WithFolder("Personnages")
        .WithFolder("PNJ", under: "Personnages")
        .WithPage("Ilsa Varenne", under: "PNJ", id: "3F2A9C14E07B5D21")
        .WithPage("Dialogues", under: "Ilsa Varenne")
        .WithPage("Inventaire", under: "Ilsa Varenne")
        .WithPage("Capitaine Orsen", under: "PNJ", id: "7A1C4E90B3F2D615")
        .WithFolder("Lieux")
        .WithPage("Hautbois", under: "Lieux", id: "5B19C0E2A4D7F311")
        .WithPage("Col gelé", under: "Lieux", id: "8C3E1F7A90B2D446")
        .WithFolder("Factions")
        .WithCategory("PNJ (catégorie)", id: "0D7EA95162C3B804");

    // F02.R1 ─ roots

    [Fact]
    public void CreateEmpty_NewProject_HasFourRootsInOrder()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Nodes.Where(n => n.IsRoot).OrderBy(n => n.SortOrder).Select(n => n.Id)
            .ShouldBe([Roots.Pages.Id, Roots.Categories.Id, Roots.Properties.Id, Roots.Links.Id]);
    }

    [Fact]
    public void Rename_Root_IsRefused()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Rename(Roots.Pages.Id, "Autre").Error.Code.ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public void Move_Root_IsRefused()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Move(Roots.Categories.Id, Roots.Pages.Id).Error.Code.ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public void Remove_Root_IsRefused()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Remove(Roots.Links.Id).Error.Code.ShouldBe(ErrorCodes.RootImmutable);
    }

    [Fact]
    public void Create_Root_IsRefused()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Create(ElementId.NewRandom(), ElementKind.Root, Roots.Pages.Id, "Racine").Error.Code.ShouldBe(ErrorCodes.KindNotCreatable);
    }

    [Theory]
    [InlineData("fr", "Catégories")]
    [InlineData("en", "Categories")]
    [InlineData("de", "Kategorien")]
    [InlineData("es-MX", "Categorías")]
    [InlineData("it", "Categories")]
    public void Label_ContentLanguage_IsTranslatedWithEnglishFallback(string language, string expected)
    {
        Roots.Label(Roots.Categories, language).ShouldBe(expected);
    }

    // F02.R2 ─ stay under the own root

    [Fact]
    public void Move_CategoryUnderPages_IsRefusedAsCrossRoot()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("PNJ (catégorie)"), Roots.Pages.Id).Error.Code.ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public void Move_PageIntoCategoryFolder_IsRefusedAsCrossRoot()
    {
        var builder = Chroniques().WithFolder("Monde", root: ElementKind.Category);
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Hautbois"), builder.IdOf("Monde")).Error.Code.ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public void Create_PageUnderCategoriesRoot_IsRefused()
    {
        var tree = ElementTree.CreateEmpty();

        tree.Create(ElementId.NewRandom(), ElementKind.Page, Roots.Categories.Id, "Perdue").Error.Code.ShouldBe(ErrorCodes.MoveCrossRoot);
    }

    [Fact]
    public void Move_PageToAnotherFolderOfPages_Succeeds()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        var result = tree.Move(builder.IdOf("Hautbois"), builder.IdOf("Factions"));

        result.IsSuccess.ShouldBeTrue();
        tree.Get(builder.IdOf("Hautbois")).ParentId.ShouldBe(builder.IdOf("Factions"));
        tree.Children(builder.IdOf("Lieux")).Select(n => n.Name).ShouldBe(["Col gelé"]);
    }

    // F02.R3 ─ sub-pages, never under an own descendant

    [Fact]
    public void Move_PageUnderAnotherPage_BecomesSubPage()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Capitaine Orsen"), builder.IdOf("Ilsa Varenne")).IsSuccess.ShouldBeTrue();

        tree.Children(builder.IdOf("Ilsa Varenne")).Select(n => n.Name).ShouldBe(["Dialogues", "Inventaire", "Capitaine Orsen"]);
    }

    [Fact]
    public void Move_PageUnderItsSubPage_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Ilsa Varenne"), builder.IdOf("Dialogues")).Error.Code.ShouldBe(ErrorCodes.MoveIntoDescendant);
    }

    [Fact]
    public void Move_FolderUnderItself_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Personnages"), builder.IdOf("Personnages")).Error.Code.ShouldBe(ErrorCodes.MoveIntoDescendant);
    }

    [Fact]
    public void Move_FolderUnderItsSubFolder_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Personnages"), builder.IdOf("PNJ")).Error.Code.ShouldBe(ErrorCodes.MoveIntoDescendant);
    }

    [Fact]
    public void Move_FolderUnderPage_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Factions"), builder.IdOf("Hautbois")).Error.Code.ShouldBe(ErrorCodes.InvalidParent);
    }

    [Fact]
    public void Move_UnknownElementOrParent_IsNotFound()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(ElementId.NewRandom(), Roots.Pages.Id).Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
        tree.Move(builder.IdOf("Hautbois"), ElementId.NewRandom()).Error.Code.ShouldBe(ErrorCodes.ParentNotFound);
    }

    [Fact]
    public void Move_Refused_LeavesTreeUnchanged()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();
        var before = tree.Nodes.OrderBy(n => n.Id.Value).ToList();

        tree.Move(builder.IdOf("Ilsa Varenne"), builder.IdOf("Inventaire")).IsSuccess.ShouldBeFalse();

        tree.Nodes.OrderBy(n => n.Id.Value).ShouldBe(before);
    }

    // Attachments

    [Fact]
    public void Create_AttachmentUnderPage_IsAllowedButNotMovable()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();
        var attachment = ElementId.NewRandom();

        tree.Create(attachment, ElementKind.Attachment, builder.IdOf("Hautbois"), "carte.webp").IsSuccess.ShouldBeTrue();

        tree.Move(attachment, builder.IdOf("Col gelé")).Error.Code.ShouldBe(ErrorCodes.NotMovable);
        tree.Create(ElementId.NewRandom(), ElementKind.Attachment, builder.IdOf("Lieux"), "x.png").Error.Code.ShouldBe(ErrorCodes.InvalidParent);
    }

    // F02.R4 ─ IDs never change

    [Fact]
    public void RenameAndMove_Element_KeepsItsId()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();
        var hautbois = builder.IdOf("Hautbois");

        tree.Rename(hautbois, "Hautbois-sur-Mer").Value.Node.Id.ShouldBe(hautbois);
        tree.Move(hautbois, builder.IdOf("Factions")).Value.Node.Id.ShouldBe(hautbois);
        tree.Get(hautbois).Name.ShouldBe("Hautbois-sur-Mer");
    }

    // F02.R6 ─ duplicate names allowed and detectable

    [Fact]
    public void Create_SiblingWithSameName_IsAllowedAndDetected()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();
        var twin = ElementId.NewRandom();

        tree.Create(twin, ElementKind.Page, builder.IdOf("Lieux"), "  hautbois ").IsSuccess.ShouldBeTrue();

        tree.HasDuplicateName(twin).ShouldBeTrue();
        tree.HasDuplicateName(builder.IdOf("Hautbois")).ShouldBeTrue();
        tree.HasDuplicateName(builder.IdOf("Col gelé")).ShouldBeFalse();
    }

    [Fact]
    public void DuplicateNames_AnyTree_AgreesWithHasDuplicateNameForEveryNode()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();
        tree.Create(ElementId.NewRandom(), ElementKind.Page, builder.IdOf("Lieux"), "  hautbois ").IsSuccess.ShouldBeTrue();
        tree.Create(ElementId.NewRandom(), ElementKind.Page, builder.IdOf("PNJ"), "capitaine ORSEN").IsSuccess.ShouldBeTrue();
        tree.Create(ElementId.NewRandom(), ElementKind.Page, builder.IdOf("Lieux"), "Autre").IsSuccess.ShouldBeTrue();

        var duplicates = tree.DuplicateNames();

        tree.Nodes.ShouldAllBe(n => duplicates.Contains(n.Id) == tree.HasDuplicateName(n.Id));
        duplicates.ShouldContain(builder.IdOf("Hautbois"));
        duplicates.Count.ShouldBe(4);
    }

    [Fact]
    public void HasDuplicateName_SameNameInAnotherFolder_IsFalse()
    {
        var builder = Chroniques().WithPage("Hautbois bis", under: "Factions");
        var tree = builder.BuildTree();
        tree.Rename(builder.IdOf("Hautbois bis"), "Hautbois");

        tree.HasDuplicateName(builder.IdOf("Hautbois")).ShouldBeFalse();
    }

    // Names

    [Theory]
    [InlineData("", ErrorCodes.NameEmpty)]
    [InlineData("   ", ErrorCodes.NameEmpty)]
    [InlineData("a\u0001b", ErrorCodes.NameInvalid)]
    public void Rename_InvalidName_IsRefused(string name, string code)
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Rename(builder.IdOf("Hautbois"), name).Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Rename_TooLongName_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Rename(builder.IdOf("Hautbois"), new string('a', ElementName.MaxLength + 1)).Error.Code.ShouldBe(ErrorCodes.NameTooLong);
    }

    [Fact]
    public void Create_ExistingId_IsRefused()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Create(builder.IdOf("Hautbois"), ElementKind.Page, Roots.Pages.Id, "Copie").Error.Code.ShouldBe(ErrorCodes.IdAlreadyUsed);
    }

    // Order of siblings

    [Fact]
    public void Move_WithIndex_ReordersSiblings()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Move(builder.IdOf("Col gelé"), builder.IdOf("Lieux"), index: 0).IsSuccess.ShouldBeTrue();

        tree.Children(builder.IdOf("Lieux")).Select(n => n.Name).ShouldBe(["Col gelé", "Hautbois"]);
    }

    [Fact]
    public void Create_ManyAtSamePosition_RenumbersAndKeepsOrder()
    {
        var tree = ElementTree.CreateEmpty();
        var expected = new List<ElementId>();
        for (var i = 0; i < 100; i++)
        {
            var id = ElementId.NewRandom();
            // Always insert in second position: exhausts the gap and forces renumbering.
            var index = Math.Min(1, expected.Count);
            tree.Create(id, ElementKind.Page, Roots.Pages.Id, $"Page {i}", index).IsSuccess.ShouldBeTrue();
            expected.Insert(index, id);
        }

        tree.Children(Roots.Pages.Id).Select(n => n.Id).ShouldBe(expected);
        tree.Children(Roots.Pages.Id).Select(n => n.SortOrder).ShouldBeUnique();
    }

    [Fact]
    public void Remove_Page_RemovesSubPages()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        var removed = tree.Remove(builder.IdOf("Ilsa Varenne")).Value;

        removed.Select(n => n.Name).ShouldBe(["Ilsa Varenne", "Inventaire", "Dialogues"], ignoreOrder: true);
        tree.Contains(builder.IdOf("Dialogues")).ShouldBeFalse();
        tree.Children(builder.IdOf("PNJ")).Select(n => n.Name).ShouldBe(["Capitaine Orsen"]);
    }

    [Fact]
    public void Ancestors_Page_AreParentFirstUpToRoot()
    {
        var builder = Chroniques();
        var tree = builder.BuildTree();

        tree.Ancestors(builder.IdOf("Dialogues")).Select(n => n.Name)
            .ShouldBe(["Ilsa Varenne", "PNJ", "Personnages", "pages"]);
        tree.RootOf(builder.IdOf("Dialogues")).ShouldBe(Roots.Pages);
    }

    // Stored data checks (erable doctor)

    [Fact]
    public void FindAnomalies_ConsistentTree_IsEmpty()
    {
        Chroniques().BuildTree().FindAnomalies().ShouldBeEmpty();
    }

    [Fact]
    public void FindAnomalies_CorruptedNodes_AreReported()
    {
        var a = new ElementId(0x1000A);
        var b = new ElementId(0x1000B);
        var orphan = new ElementId(0x1000C);
        var misplaced = new ElementId(0x1000D);
        var tree = ElementTree.Load(
        [
            new TreeNode(a, ElementKind.Folder, b, "A", 1),
            new TreeNode(b, ElementKind.Folder, a, "B", 1),
            new TreeNode(orphan, ElementKind.Page, new ElementId(0x1000E), "Orpheline", 1),
            new TreeNode(misplaced, ElementKind.Category, Roots.Pages.Id, "Égarée", 1),
        ]);

        tree.FindAnomalies().ShouldBe(
        [
            new TreeAnomaly(a, TreeAnomalyKind.Cycle),
            new TreeAnomaly(b, TreeAnomalyKind.Cycle),
            new TreeAnomaly(orphan, TreeAnomalyKind.MissingParent),
            new TreeAnomaly(misplaced, TreeAnomalyKind.OutsideOfRoot),
        ]);
    }
}
