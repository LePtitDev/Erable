using CsCheck;
using Erable.Core.Elements;
using Erable.Core.Tree;

namespace Erable.Core.Tests.Tree;

/// <summary>docs/testing.md §4.1 (Arbre): after any sequence of operations, the tree stays well formed.</summary>
public sealed class ElementTreePropertyTests
{
    private static readonly ElementKind[] CreatableKinds =
    [
        ElementKind.Folder, ElementKind.Page, ElementKind.Category,
        ElementKind.PropertyDefinition, ElementKind.LinkType, ElementKind.Attachment,
    ];

    /// <summary>An operation whose operands are indices into the current list of nodes.</summary>
    private sealed record Operation(int Type, int Subject, int Target, int Kind, int Position);

    private static readonly Gen<Operation> GenOperation =
        Gen.Select(Gen.Int[0, 3], Gen.Int[0, 1000], Gen.Int[0, 1000], Gen.Int[0, CreatableKinds.Length - 1], Gen.Int[0, 10],
            (type, subject, target, kind, position) => new Operation(type, subject, target, kind, position));

    [Fact]
    public void AnySequenceOfOperations_KeepsTreeWellFormed()
    {
        GenOperation.List[1, 80].Sample(operations =>
        {
            var tree = ElementTree.CreateEmpty();
            var counter = 0x10000UL;
            foreach (var op in operations)
            {
                var nodes = tree.Nodes.OrderBy(n => n.Id.Value).ToList();
                var subject = nodes[op.Subject % nodes.Count];
                var target = nodes[op.Target % nodes.Count];
                switch (op.Type)
                {
                    case 0:
                    case 1:
                        // Creation is weighted twice so that trees grow.
                        tree.Create(new ElementId(++counter), CreatableKinds[op.Kind], target.Id, $"n{counter}", op.Position);
                        break;
                    case 2:
                        tree.Move(subject.Id, target.Id, op.Position);
                        break;
                    default:
                        tree.Rename(subject.Id, subject.Name + "'");
                        break;
                }

                AssertWellFormed(tree);
            }
        });
    }

    [Fact]
    public void Move_Valid_KeepsEveryOtherParent()
    {
        GenOperation.List[1, 60].Sample(operations =>
        {
            var tree = ElementTree.CreateEmpty();
            var counter = 0x10000UL;
            foreach (var op in operations)
            {
                var nodes = tree.Nodes.OrderBy(n => n.Id.Value).ToList();
                var target = nodes[op.Target % nodes.Count];
                if (op.Type != 2)
                {
                    tree.Create(new ElementId(++counter), CreatableKinds[op.Kind], target.Id, "x");
                    continue;
                }

                var subject = nodes[op.Subject % nodes.Count];
                var parentsBefore = tree.Nodes.Where(n => n.Id != subject.Id).ToDictionary(n => n.Id, n => n.ParentId);
                tree.Move(subject.Id, target.Id);
                foreach (var (id, parent) in parentsBefore)
                {
                    tree.Get(id).ParentId.ShouldBe(parent);
                }
            }
        });
    }

    private static void AssertWellFormed(ElementTree tree)
    {
        tree.FindAnomalies().ShouldBeEmpty();

        // Each non-root node is listed exactly once, under its own parent.
        var listed = tree.Nodes.SelectMany(n => tree.Children(n.Id)).ToList();
        listed.Select(n => n.Id).ShouldBeUnique();
        listed.Count.ShouldBe(tree.Count - Roots.All.Count);
        foreach (var child in listed)
        {
            tree.Children(child.ParentId).ShouldContain(child);
        }

        // Siblings never share a sort order (their order is unambiguous).
        foreach (var node in tree.Nodes)
        {
            tree.Children(node.Id).Select(c => c.SortOrder).ShouldBeUnique();
        }
    }
}
