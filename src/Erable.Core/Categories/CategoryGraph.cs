namespace Erable.Core.Categories;

/// <summary>Inheritance link of a category: its optional single parent (docs/architecture.md §5.3, F07.R1).</summary>
/// <param name="Id">The category.</param>
/// <param name="ParentId">Parent category, <see cref="ElementId.None"/> for none.</param>
public sealed record CategoryNode(ElementId Id, ElementId ParentId);

/// <summary>
/// Inheritance graph of the categories of a project (single inheritance: Entité › Personnage › PNJ). Independent of
/// the explorer tree, where categories are only filed in folders (decision P-14). Mutating methods validate first and
/// change nothing when they fail; a reparenting that would create a cycle is refused (F07.R2).
/// </summary>
public sealed class CategoryGraph
{
    private readonly Dictionary<ElementId, ElementId> _parents = [];
    private readonly Dictionary<ElementId, HashSet<ElementId>> _children = [];

    private CategoryGraph()
    {
    }

    public int Count => _parents.Count;

    public IEnumerable<CategoryNode> Nodes => _parents.Select(p => new CategoryNode(p.Key, p.Value));

    /// <summary>
    /// Builds the graph from stored categories. The input is trusted: a parent that is not a known category is kept
    /// as is but ignored by the traversals (deleted parent); cycles in corrupted data never make a traversal loop and
    /// are reported by <see cref="FindCycles"/>.
    /// </summary>
    public static CategoryGraph Load(IEnumerable<CategoryNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        var graph = new CategoryGraph();
        foreach (var node in nodes)
        {
            if (!node.Id.IsNone && !graph._parents.ContainsKey(node.Id))
            {
                graph.Link(node.Id, node.ParentId);
            }
        }

        return graph;
    }

    public bool Contains(ElementId id) => _parents.ContainsKey(id);

    /// <summary>The parent category; <see cref="ElementId.None"/> when there is none or it is not a known category.</summary>
    public ElementId ParentOf(ElementId id) =>
        _parents.TryGetValue(id, out var parent) && _parents.ContainsKey(parent) ? parent : ElementId.None;

    /// <summary>Parent first, up to the top-most ancestor; stops on a cycle (corrupted data).</summary>
    public IReadOnlyList<ElementId> Ancestors(ElementId id)
    {
        var result = new List<ElementId>();
        var seen = new HashSet<ElementId> { id };
        var current = ParentOf(id);
        while (!current.IsNone && seen.Add(current))
        {
            result.Add(current);
            current = ParentOf(current);
        }

        return result;
    }

    /// <summary>The inheritance chain shown on the category screen: top-most ancestor first, the category last.</summary>
    public IReadOnlyList<ElementId> Lineage(ElementId id) => Contains(id) ? [.. Ancestors(id).Reverse(), id] : [];

    /// <summary>Number of ancestors: 0 for a category without parent.</summary>
    public int Depth(ElementId id) => Ancestors(id).Count;

    /// <summary>Direct sub-categories, by ID (the UI sorts them by name).</summary>
    public IReadOnlyList<ElementId> Children(ElementId id) =>
        _children.TryGetValue(id, out var children) ? [.. children.Where(Contains).OrderBy(c => c.Value)] : [];

    /// <summary>Every sub-category at any depth, excluding the category itself (breadth first).</summary>
    public IReadOnlyList<ElementId> Descendants(ElementId id)
    {
        var result = new List<ElementId>();
        var seen = new HashSet<ElementId> { id };
        var queue = new Queue<ElementId>([id]);
        while (queue.Count > 0)
        {
            foreach (var child in Children(queue.Dequeue()))
            {
                if (seen.Add(child))
                {
                    result.Add(child);
                    queue.Enqueue(child);
                }
            }
        }

        return result;
    }

    /// <summary>True when <paramref name="ancestor"/> is a strict ancestor of <paramref name="id"/>.</summary>
    public bool IsAncestorOf(ElementId ancestor, ElementId id) => Ancestors(id).Contains(ancestor);

    /// <summary>True when the two categories are on one inheritance chain (one is the other or an ancestor of it).</summary>
    public bool AreRelated(ElementId a, ElementId b) => a == b || IsAncestorOf(a, b) || IsAncestorOf(b, a);

    /// <summary>
    /// Checks that <paramref name="parentId"/> may become the parent of <paramref name="id"/>, without changing anything.
    /// A cycle is refused with the loop in <see cref="DomainError.Extensions"/> (<c>loop</c>: the category, the chain
    /// down to the new parent, the category again — « Personnage › PNJ › Marchand › Personnage », E2E-07-03).
    /// </summary>
    public DomainError? CheckParent(ElementId id, ElementId parentId)
    {
        if (!Contains(id))
        {
            return new DomainError(ErrorCodes.ElementNotFound, $"Category {id} does not exist.");
        }

        if (parentId.IsNone)
        {
            return null;
        }

        if (!Contains(parentId))
        {
            return new DomainError(ErrorCodes.CategoryParentNotFound, $"Category {parentId} does not exist.");
        }

        if (parentId != id && !IsAncestorOf(id, parentId))
        {
            return null;
        }

        // The new parent is the category itself or one of its descendants: the loop goes down from the category to it.
        List<ElementId> loop = parentId == id
            ? [id, id]
            : [id, .. Ancestors(parentId).TakeWhile(a => a != id).Reverse(), parentId, id];
        return new DomainError(
            ErrorCodes.CategoryCycle,
            $"Category {parentId} cannot be the parent of {id}: it would create a loop.",
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["loop"] = loop.Select(l => l.ToString()).ToArray() });
    }

    /// <summary>Adds a new category, with an optional parent.</summary>
    public Result<CategoryNode> Add(ElementId id, ElementId parentId)
    {
        if (id.IsNone || Contains(id))
        {
            return new DomainError(ErrorCodes.IdAlreadyUsed, $"ID {id} is already used.");
        }

        if (!parentId.IsNone && !Contains(parentId))
        {
            return new DomainError(ErrorCodes.CategoryParentNotFound, $"Category {parentId} does not exist.");
        }

        Link(id, parentId);
        return new CategoryNode(id, parentId);
    }

    /// <summary>Changes (or removes, with <see cref="ElementId.None"/>) the parent of a category (F07.R2).</summary>
    public Result<CategoryNode> SetParent(ElementId id, ElementId parentId)
    {
        if (CheckParent(id, parentId) is { } error)
        {
            return error;
        }

        Unlink(id);
        Link(id, parentId);
        return new CategoryNode(id, parentId);
    }

    /// <summary>
    /// Removes a category (trash, M2-08). Its sub-categories keep pointing to it: while it is absent they have no
    /// parent, and restoring it restores the chain unchanged.
    /// </summary>
    public bool Remove(ElementId id)
    {
        if (!Contains(id))
        {
            return false;
        }

        Unlink(id);
        _parents.Remove(id);
        return true;
    }

    /// <summary>Categories that are part of a parent cycle (stored data only; used by <c>erable doctor</c>).</summary>
    public IReadOnlyList<ElementId> FindCycles()
    {
        var result = new List<ElementId>();
        foreach (var id in _parents.Keys.OrderBy(i => i.Value))
        {
            var seen = new HashSet<ElementId>();
            var current = id;
            while (!current.IsNone && seen.Add(current))
            {
                current = ParentOf(current);
            }

            if (current == id)
            {
                result.Add(id);
            }
        }

        return result;
    }

    private void Link(ElementId id, ElementId parentId)
    {
        _parents[id] = parentId;
        if (!parentId.IsNone)
        {
            if (!_children.TryGetValue(parentId, out var children))
            {
                children = [];
                _children[parentId] = children;
            }

            children.Add(id);
        }
    }

    private void Unlink(ElementId id)
    {
        if (_parents.TryGetValue(id, out var parent) && _children.TryGetValue(parent, out var siblings))
        {
            siblings.Remove(id);
        }
    }
}
