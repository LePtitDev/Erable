using System.Diagnostics;
using Erable.Core.Elements;

namespace Erable.Core.Tree;

/// <summary>Structural part of an element: what the tree rules need.</summary>
public sealed record TreeNode(ElementId Id, ElementKind Kind, ElementId ParentId, string Name, long SortOrder)
{
    public bool IsRoot => Kind == ElementKind.Root;
}

/// <summary>Nodes whose parent, name or position changed in one operation; what storage must write.</summary>
public sealed record TreeChange(TreeNode Node, IReadOnlyList<TreeNode> Changed);

/// <summary>
/// In-memory explorer tree applying the rules of F02 (docs/features.md), independent of storage. Live (not deleted)
/// elements only. Mutating methods validate first and change nothing when they fail.
/// </summary>
public sealed class ElementTree
{
    private readonly Dictionary<ElementId, TreeNode> _nodes = [];
    private readonly Dictionary<ElementId, List<ElementId>> _children = [];

    private ElementTree()
    {
    }

    public int Count => _nodes.Count;

    public IEnumerable<TreeNode> Nodes => _nodes.Values;

    /// <summary>A new project: the four roots only.</summary>
    public static ElementTree CreateEmpty() => Load([]);

    /// <summary>
    /// Builds the tree from stored nodes. The roots are always present (added when missing). The input is trusted
    /// (it comes from the project); <see cref="FindAnomalies"/> reports inconsistencies without failing.
    /// </summary>
    public static ElementTree Load(IEnumerable<TreeNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var tree = new ElementTree();
        foreach (var root in Roots.All)
        {
            tree.Add(new TreeNode(root.Id, ElementKind.Root, ElementId.None, root.Key, SortOrderOf(root)));
        }

        foreach (var node in nodes)
        {
            if (!tree._nodes.ContainsKey(node.Id))
            {
                tree.Add(node);
            }
        }

        return tree;
    }

    public TreeNode? Find(ElementId id) => _nodes.GetValueOrDefault(id);

    public TreeNode Get(ElementId id) =>
        _nodes.TryGetValue(id, out var node) ? node : throw new KeyNotFoundException($"Element {id} is not in the tree.");

    public bool Contains(ElementId id) => _nodes.ContainsKey(id);

    /// <summary>Children ordered by sort order, then ID (deterministic when two orders are equal).</summary>
    public IReadOnlyList<TreeNode> Children(ElementId id) =>
        _children.TryGetValue(id, out var ids)
            ? [.. ids.Select(c => _nodes[c]).OrderBy(n => n.SortOrder).ThenBy(n => n.Id.Value)]
            : [];

    /// <summary>Parent first, up to the root; stops on a cycle or a missing parent (stored data may be corrupted).</summary>
    public IReadOnlyList<TreeNode> Ancestors(ElementId id)
    {
        var result = new List<TreeNode>();
        var seen = new HashSet<ElementId> { id };
        var current = Find(id);
        while (current is not null && !current.ParentId.IsNone && seen.Add(current.ParentId)
               && _nodes.TryGetValue(current.ParentId, out var parent))
        {
            result.Add(parent);
            current = parent;
        }

        return result;
    }

    /// <summary>Root of an element, or null when its chain is broken.</summary>
    public RootDefinition? RootOf(ElementId id)
    {
        var node = Find(id);
        if (node is null)
        {
            return null;
        }

        if (node.IsRoot)
        {
            return Roots.Find(node.Id);
        }

        var top = Ancestors(id) is [.., var last] ? last : null;
        return top is { IsRoot: true } ? Roots.Find(top.Id) : null;
    }

    /// <summary>True when <paramref name="candidate"/> is <paramref name="ancestor"/> or one of its descendants.</summary>
    public bool IsSelfOrDescendant(ElementId candidate, ElementId ancestor) =>
        candidate == ancestor || Ancestors(candidate).Any(a => a.Id == ancestor);

    /// <summary>All descendants (depth first), excluding the element itself.</summary>
    public IEnumerable<TreeNode> Descendants(ElementId id)
    {
        var stack = new Stack<ElementId>([id]);
        var seen = new HashSet<ElementId> { id };
        while (stack.Count > 0)
        {
            foreach (var child in Children(stack.Pop()))
            {
                if (seen.Add(child.Id))
                {
                    yield return child;
                    stack.Push(child.Id);
                }
            }
        }
    }

    /// <summary>True when a sibling bears the same name (F02.R6).</summary>
    public bool HasDuplicateName(ElementId id)
    {
        var node = Find(id);
        return node is not null && !node.IsRoot
            && Children(node.ParentId).Any(s => s.Id != id && ElementName.AreDuplicates(s.Name, node.Name));
    }

    /// <summary>
    /// Every element that shares its name with a sibling, found in one pass over the tree: the same answer as
    /// <see cref="HasDuplicateName"/> for each node, without listing the siblings of each of them (10 000 nodes in the performance budgets).
    /// </summary>
    public HashSet<ElementId> DuplicateNames()
    {
        var byParent = new Dictionary<ElementId, Dictionary<string, List<ElementId>>>();
        foreach (var node in _nodes.Values.Where(n => !n.IsRoot))
        {
            if (!byParent.TryGetValue(node.ParentId, out var names))
            {
                byParent[node.ParentId] = names = new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);
            }

            var key = node.Name.Trim();
            if (!names.TryGetValue(key, out var ids))
            {
                names[key] = ids = [];
            }

            ids.Add(node.Id);
        }

        return [.. byParent.Values.SelectMany(names => names.Values).Where(ids => ids.Count > 1).SelectMany(ids => ids)];
    }

    /// <summary>Checks that <paramref name="kind"/> may live under <paramref name="parentId"/>, without changing anything.</summary>
    public DomainError? CheckParent(ElementKind kind, ElementId parentId)
    {
        var parent = Find(parentId);
        if (parent is null)
        {
            return new DomainError(ErrorCodes.ParentNotFound, $"Parent {parentId} does not exist.");
        }

        if (kind == ElementKind.Attachment)
        {
            return parent.Kind == ElementKind.Page
                ? null
                : new DomainError(ErrorCodes.InvalidParent, "An attachment belongs to a page.");
        }

        var root = RootOf(parentId);
        if (root is null)
        {
            return new DomainError(ErrorCodes.InvalidParent, $"Parent {parentId} is not attached to a root.");
        }

        // Folders take the kind of their root; everything else must match its root (F02.R2).
        if (kind != ElementKind.Folder && kind != root.ItemKind)
        {
            return new DomainError(ErrorCodes.MoveCrossRoot, $"A {kind} cannot be placed under the '{root.Key}' root.");
        }

        var allowed = parent.Kind switch
        {
            ElementKind.Root or ElementKind.Folder => true,
            // A sub-page is a page under a page (docs/architecture.md §5.1); nothing else goes under a page.
            ElementKind.Page => kind == ElementKind.Page,
            ElementKind.Category or ElementKind.PropertyDefinition or ElementKind.LinkType or ElementKind.Attachment => false,
            _ => throw new UnreachableException($"Unknown element kind {parent.Kind}."),
        };
        return allowed ? null : new DomainError(ErrorCodes.InvalidParent, $"A {parent.Kind} cannot contain a {kind}.");
    }

    /// <summary>Checks a move without changing anything; used for drag and drop feedback and by <see cref="Move"/>.</summary>
    public DomainError? CheckMove(ElementId id, ElementId newParentId)
    {
        var node = Find(id);
        if (node is null)
        {
            return new DomainError(ErrorCodes.ElementNotFound, $"Element {id} does not exist.");
        }

        if (node.IsRoot)
        {
            return new DomainError(ErrorCodes.RootImmutable, "Roots cannot be moved.");
        }

        if (node.Kind == ElementKind.Attachment)
        {
            return new DomainError(ErrorCodes.NotMovable, "Attachments are not moved through the tree.");
        }

        var target = Find(newParentId);
        if (target is null)
        {
            return new DomainError(ErrorCodes.ParentNotFound, $"Parent {newParentId} does not exist.");
        }

        // Cross-root before descendant: a different root cannot be a descendant anyway, and its message is clearer.
        if (RootOf(newParentId) is { } targetRoot && RootOf(id) is { } currentRoot && targetRoot.Id != currentRoot.Id)
        {
            return new DomainError(ErrorCodes.MoveCrossRoot, $"Element {id} must stay under the '{currentRoot.Key}' root.");
        }

        if (IsSelfOrDescendant(newParentId, id))
        {
            return new DomainError(ErrorCodes.MoveIntoDescendant, $"Element {id} cannot be moved under itself or its descendants.");
        }

        return CheckParent(node.Kind, newParentId);
    }

    /// <summary>Adds a new element at <paramref name="index"/> among its future siblings (end when null).</summary>
    public Result<TreeChange> Create(ElementId id, ElementKind kind, ElementId parentId, string name, int? index = null)
    {
        if (id.IsNone || _nodes.ContainsKey(id))
        {
            return new DomainError(ErrorCodes.IdAlreadyUsed, $"ID {id} is already used.");
        }

        if (kind == ElementKind.Root)
        {
            return new DomainError(ErrorCodes.KindNotCreatable, "Roots are fixed.");
        }

        var normalized = ElementName.Normalize(name);
        if (!normalized.IsSuccess)
        {
            return normalized.Error;
        }

        if (CheckParent(kind, parentId) is { } error)
        {
            return error;
        }

        var node = new TreeNode(id, kind, parentId, normalized.Value, 0);
        var changed = Place(node, parentId, index);
        return new TreeChange(_nodes[id], changed);
    }

    public Result<TreeChange> Rename(ElementId id, string name)
    {
        var node = Find(id);
        if (node is null)
        {
            return new DomainError(ErrorCodes.ElementNotFound, $"Element {id} does not exist.");
        }

        if (node.IsRoot)
        {
            return new DomainError(ErrorCodes.RootImmutable, "Roots cannot be renamed.");
        }

        var normalized = ElementName.Normalize(name);
        if (!normalized.IsSuccess)
        {
            return normalized.Error;
        }

        var renamed = node with { Name = normalized.Value };
        _nodes[id] = renamed;
        return new TreeChange(renamed, [renamed]);
    }

    /// <summary>
    /// Moves (or reorders, when the parent does not change) an element to <paramref name="index"/> among the
    /// siblings it will have, the element itself excluded; at the end when null.
    /// </summary>
    public Result<TreeChange> Move(ElementId id, ElementId newParentId, int? index = null)
    {
        if (CheckMove(id, newParentId) is { } error)
        {
            return error;
        }

        var node = _nodes[id];
        Detach(node);
        var changed = Place(node, newParentId, index);
        return new TreeChange(_nodes[id], changed);
    }

    /// <summary>Removes an element and its descendants (trash); returns the removed nodes.</summary>
    public Result<IReadOnlyList<TreeNode>> Remove(ElementId id)
    {
        var node = Find(id);
        if (node is null)
        {
            return new DomainError(ErrorCodes.ElementNotFound, $"Element {id} does not exist.");
        }

        if (node.IsRoot)
        {
            return new DomainError(ErrorCodes.RootImmutable, "Roots cannot be deleted.");
        }

        List<TreeNode> removed = [node, .. Descendants(id)];
        Detach(node);
        foreach (var r in removed)
        {
            _nodes.Remove(r.Id);
            _children.Remove(r.Id);
        }

        return removed;
    }

    /// <summary>
    /// Structural inconsistencies of stored data (used by <c>erable doctor</c>): missing parent, cycle, element
    /// outside of its root, invalid parent kind.
    /// </summary>
    public IReadOnlyList<TreeAnomaly> FindAnomalies()
    {
        var anomalies = new List<TreeAnomaly>();
        foreach (var node in _nodes.Values.Where(n => !n.IsRoot).OrderBy(n => n.Id.Value))
        {
            if (!_nodes.ContainsKey(node.ParentId))
            {
                anomalies.Add(new TreeAnomaly(node.Id, TreeAnomalyKind.MissingParent));
            }
            else if (IsInCycle(node.Id))
            {
                anomalies.Add(new TreeAnomaly(node.Id, TreeAnomalyKind.Cycle));
            }
            else if (RootOf(node.Id) is null)
            {
                anomalies.Add(new TreeAnomaly(node.Id, TreeAnomalyKind.Detached));
            }
            else if (CheckParent(node.Kind, node.ParentId) is not null)
            {
                anomalies.Add(new TreeAnomaly(node.Id, TreeAnomalyKind.OutsideOfRoot));
            }
        }

        return anomalies;
    }

    private bool IsInCycle(ElementId id)
    {
        var seen = new HashSet<ElementId>();
        var current = id;
        while (_nodes.TryGetValue(current, out var node) && !node.ParentId.IsNone)
        {
            if (!seen.Add(current))
            {
                return true;
            }

            current = node.ParentId;
        }

        return false;
    }

    private void Add(TreeNode node)
    {
        _nodes[node.Id] = node;
        if (!node.ParentId.IsNone)
        {
            ChildrenList(node.ParentId).Add(node.Id);
        }
    }

    private List<ElementId> ChildrenList(ElementId parentId)
    {
        if (!_children.TryGetValue(parentId, out var list))
        {
            list = [];
            _children[parentId] = list;
        }

        return list;
    }

    private void Detach(TreeNode node)
    {
        if (_children.TryGetValue(node.ParentId, out var siblings))
        {
            siblings.Remove(node.Id);
        }
    }

    /// <summary>Inserts the (detached) node under its new parent and computes sort orders; renumbers when needed.</summary>
    private List<TreeNode> Place(TreeNode node, ElementId parentId, int? index)
    {
        var siblings = Children(parentId);
        var position = Math.Clamp(index ?? siblings.Count, 0, siblings.Count);
        var before = position > 0 ? siblings[position - 1].SortOrder : (long?)null;
        var after = position < siblings.Count ? siblings[position].SortOrder : (long?)null;

        var changed = new List<TreeNode>();
        if (SortOrder.Between(before, after) is { } order)
        {
            var placed = node with { ParentId = parentId, SortOrder = order };
            Add(placed);
            changed.Add(placed);
            return changed;
        }

        // No room left: renumber every sibling, the node included.
        var ordered = siblings.ToList();
        ordered.Insert(position, node with { ParentId = parentId });
        Add(node with { ParentId = parentId });
        foreach (var (sibling, value) in ordered.Zip(SortOrder.Renumber(ordered.Count)))
        {
            var updated = sibling with { SortOrder = value };
            _nodes[sibling.Id] = updated;
            changed.Add(updated);
        }

        return changed;
    }

    private static long SortOrderOf(RootDefinition root) =>
        (Roots.All.ToList().IndexOf(root) + 1) * SortOrder.Step;
}

public enum TreeAnomalyKind
{
    /// <summary>The parent does not exist (or is deleted while the child is not).</summary>
    MissingParent,

    /// <summary>The element is part of a parent cycle.</summary>
    Cycle,

    /// <summary>The chain of parents does not end on a root.</summary>
    Detached,

    /// <summary>The element is under a root or a parent that cannot contain its kind.</summary>
    OutsideOfRoot,
}

public sealed record TreeAnomaly(ElementId Id, TreeAnomalyKind Kind);
