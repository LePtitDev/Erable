using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Tree;

namespace Erable.Storage;

/// <summary>An element, by ID and name.</summary>
public sealed record ImpactItem(ElementId Id, string Name);

/// <summary>
/// What deleting an element would take with it (F12.R2): the pages that mention it, its sub-pages, the typed links,
/// categories and values that would go.
/// </summary>
/// <param name="ReferencedBy">Live pages outside the deletion that mention the element or something below it.</param>
/// <param name="SubPages">Pages below the element.</param>
/// <param name="Descendants">Every element below it (sub-pages, folder contents, attachments).</param>
/// <param name="Links">Typed links that disappear (those of a link type, those of a page).</param>
/// <param name="Pages">Pages that lose a deleted category.</param>
/// <param name="Values">Owners whose value of a deleted property is hidden.</param>
public sealed record DeleteImpact(
    Element Element, IReadOnlyList<ImpactItem> ReferencedBy, int SubPages, int Descendants, int Links, int Pages, int Values);

/// <summary>One deletion in the trash (F12): what the user deleted, with what came along.</summary>
/// <param name="Path">The original ancestors, root first (they may be in the trash themselves).</param>
/// <param name="ParentAvailable">The original parent is still in the project: restoring puts the entry back there.</param>
public sealed record TrashEntry(
    ElementId Id, ElementKind Kind, string Name, DateTimeOffset DeletedAt, string DeletedBy, int Descendants,
    IReadOnlyList<TreeNode> Path, bool ParentAvailable);

/// <summary>An entry brought back; <paramref name="Relocated"/> when its parent was gone and it went to a root (F12.R3).</summary>
public sealed record RestoreResult(Element Element, ElementId ParentId, bool Relocated, int Restored);

public sealed partial class ProjectService
{
    // ─── Reads ──────────────────────────────────────────────────────────────

    /// <summary>What deleting an element would do; null when it is not a live element.</summary>
    public DeleteImpact? GetDeleteImpact(ElementId id)
    {
        using var connection = Project.Database.OpenConnection();
        var tree = Repository.LoadTree(connection);
        if (tree.Find(id) is not { } node || Repository.GetElement(id, connection) is not { } element)
        {
            return null;
        }

        var below = tree.Descendants(id).ToList();
        var subtree = new HashSet<ElementId>([id, .. below.Select(n => n.Id)]);
        var json = StructureRepository.Json(subtree);
        var mentioning = connection.Query<long>(
            """
            SELECT DISTINCT r.source_id FROM refs r JOIN elements e ON e.id = r.source_id
            WHERE r.target_id IN (SELECT value FROM json_each(@Ids)) AND e.deleted_at IS NULL AND e.kind = 'page'
            """,
            new { Ids = json }).Select(ElementId.FromInt64)
            // An attachment is used by its own page by definition: only the other pages count (E2E-06-06).
            .Where(source => !subtree.Contains(source) && !(node.Kind == ElementKind.Attachment && source == node.ParentId))
            .Select(source => new ImpactItem(source, tree.Find(source)?.Name ?? source.ToString()))
            .OrderBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Id.Value)
            .ToArray();

        var links = LiveLinks(connection, tree)
            .Count(l => subtree.Contains(l.LinkTypeId) || subtree.Contains(l.SourceId) || subtree.Contains(l.TargetId));

        var categories = below.Where(n => n.Kind == ElementKind.Category).Select(n => n.Id)
            .Concat(node.Kind == ElementKind.Category ? [id] : []).ToList();
        var state = StructureState.Load(connection, []);
        var pages = categories.SelectMany(c => AffectedPages(connection, state, c)).Distinct().Count(p => tree.Find(p) is { Kind: ElementKind.Page });

        var properties = below.Where(n => n.Kind == ElementKind.PropertyDefinition).Select(n => n.Id)
            .Concat(node.Kind == ElementKind.PropertyDefinition ? [id] : []).ToList();
        var values = properties.Count == 0
            ? 0
            : connection.ExecuteScalar<int>(
                """
                SELECT COUNT(DISTINCT v.owner_id) FROM property_values v JOIN elements e ON e.id = v.owner_id
                WHERE v.property_id IN (SELECT value FROM json_each(@Ids)) AND e.deleted_at IS NULL
                """,
                new { Ids = StructureRepository.Json(properties) });

        return new DeleteImpact(element, mentioning, below.Count(n => n.Kind == ElementKind.Page), below.Count, links, pages, values);
    }

    /// <summary>The trash, newest deletion first.</summary>
    public IReadOnlyList<TrashEntry> ListTrash()
    {
        using var connection = Project.Database.OpenConnection();
        var trash = TrashRepository.LoadTrash(connection);
        var skeleton = TrashRepository.LoadSkeleton(connection);
        var tree = Repository.LoadTree(connection);
        var members = trash.GroupBy(t => t.DeletedRoot).ToDictionary(g => g.Key, g => g.Count());
        return [.. trash.Where(t => t.Id == t.DeletedRoot).Select(t => new TrashEntry(
            t.Id, t.Kind, t.Name, t.DeletedAt, t.DeletedBy, members.GetValueOrDefault(t.Id) - 1,
            PathNodes(skeleton, t.ParentId), !t.Orphan && tree.Contains(t.ParentId)))];
    }

    private static TreeNode[] PathNodes(Dictionary<ElementId, (ElementId Parent, string Name, ElementKind Kind)> skeleton, ElementId start)
    {
        var nodes = new List<TreeNode>();
        var current = start;
        while (!current.IsNone && skeleton.TryGetValue(current, out var node) && nodes.Count < 64)
        {
            nodes.Add(new TreeNode(current, node.Kind, node.Parent, node.Name, 0));
            current = node.Parent;
        }

        nodes.Reverse();
        return [.. nodes];
    }

    // ─── Delete ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Puts an element and everything below it in the trash as one entry (F12.R1). Categories leave their pages, a
    /// property's values and a link type's links are hidden, all restorable with the element (F12.R4).
    /// </summary>
    public Task<Result<Element>> DeleteElementAsync(ElementId id, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            if (tree.Find(id) is not { } node)
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.ElementNotFound, $"Element {id} does not exist."));
            }

            if (node.IsRoot)
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.RootImmutable, "Roots cannot be deleted."));
            }

            // What the deletion changes for the derived values, known before the elements leave the tree.
            var below = new[] { node }.Concat(tree.Descendants(id)).ToList();
            var state = StructureState.Load(connection, []);
            var affected = below.Where(n => n.Kind == ElementKind.Category)
                .SelectMany(n => AffectedPages(connection, state, n.Id)).Distinct().ToList();
            var properties = below.Where(n => n.Kind == ElementKind.PropertyDefinition).Select(n => n.Id).ToList();
            var gonePages = below.Where(n => n.Kind == ElementKind.Page).Select(n => n.Id).ToList();

            var removed = tree.Remove(id);
            if (!removed.IsSuccess)
            {
                return Result.Failure<Element>(removed.Error);
            }

            var now = clock.GetUtcNow();
            TrashRepository.MarkDeleted(connection, transaction, removed.Value.Select(n => n.Id), id, now, author);
            if (gonePages.Count > 0)
            {
                StructureRepository.ReplaceDerived(connection, transaction, gonePages, null, [], []);
            }

            affected = [.. affected.Except(gonePages)];
            if (affected.Count > 0)
            {
                StructureState.Load(connection, affected).WriteDerived(connection, transaction, affected, null);
            }

            if (properties.Count > 0)
            {
                StructureState.Load(connection, null).WriteDerived(connection, transaction, null, properties);
            }

            return Result.Success(Repository.GetElement(id, connection)!);
        }, cancellationToken);

    // ─── Restore ────────────────────────────────────────────────────────────

    /// <summary>
    /// Brings an entry back with everything that was deleted with it, under its original parent, or under the root of
    /// its kind when that parent is gone (F12.R3). The ID does not change, so references work again.
    /// </summary>
    public Task<Result<RestoreResult>> RestoreAsync(ElementId entryId, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var trash = TrashRepository.LoadTrash(connection);
            if (trash.FirstOrDefault(t => t.Id == entryId && t.DeletedRoot == entryId) is not { } entry)
            {
                return Result.Failure<RestoreResult>(new DomainError(ErrorCodes.TrashEntryNotFound, $"Element {entryId} is not in the trash."));
            }

            var members = trash.Where(t => t.DeletedRoot == entryId).ToList();
            var tree = Repository.LoadTree(connection);
            var keep = !entry.Orphan && tree.Contains(entry.ParentId) && tree.CheckParent(entry.Kind, entry.ParentId) is null;
            ElementId? newParent = null;
            long? sort = null;
            if (!keep)
            {
                if (entry.Kind == ElementKind.Attachment)
                {
                    return Result.Failure<RestoreResult>(new DomainError(ErrorCodes.TrashParentDeleted, "The page of this attachment is in the trash: restore it first."));
                }

                var skeleton = TrashRepository.LoadSkeleton(connection);
                newParent = RootOfChain(skeleton, entry.ParentId) ?? DefaultRootOf(entry.Kind);
                sort = TrashRepository.LastSortOrder(connection, newParent.Value) + SortOrder.Step;
            }

            TrashRepository.MarkRestored(connection, transaction, entryId, newParent, sort);

            // Derived values of what came back: its pages, the pages of its categories, every page for its properties.
            var state = StructureState.Load(connection, []);
            var pages = members.Where(m => m.Kind == ElementKind.Page).Select(m => m.Id)
                .Concat(members.Where(m => m.Kind == ElementKind.Category).SelectMany(m => AffectedPages(connection, state, m.Id)))
                .Distinct().ToList();
            if (pages.Count > 0)
            {
                StructureState.Load(connection, pages).WriteDerived(connection, transaction, pages, null);
            }

            var properties = members.Where(m => m.Kind == ElementKind.PropertyDefinition).Select(m => m.Id).ToList();
            if (properties.Count > 0)
            {
                StructureState.Load(connection, null).WriteDerived(connection, transaction, null, properties);
            }

            var element = Repository.GetElement(entryId, connection)!;
            return Result.Success(new RestoreResult(element, element.ParentId, !keep, members.Count));
        }, cancellationToken);

    private static ElementId? RootOfChain(Dictionary<ElementId, (ElementId Parent, string Name, ElementKind Kind)> skeleton, ElementId start)
    {
        var current = start;
        for (var guard = 0; guard < 256 && !current.IsNone && skeleton.TryGetValue(current, out var node); guard++)
        {
            if (node.Kind == ElementKind.Root)
            {
                return current;
            }

            current = node.Parent;
        }

        return null;
    }

    private static ElementId DefaultRootOf(ElementKind kind) => kind switch
    {
        ElementKind.Category => Roots.Categories.Id,
        ElementKind.PropertyDefinition => Roots.Properties.Id,
        ElementKind.LinkType => Roots.Links.Id,
        _ => Roots.Pages.Id,
    };

    // ─── Empty ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Removes entries of the trash for good (F12.R5), or all of them when none is given: their content, values, links,
    /// revisions and attachments go, and blobs nothing references any more are collected. Other entries that hung under a
    /// removed one go to the root of their kind.
    /// </summary>
    public Task<Result<int>> PurgeAsync(IReadOnlyCollection<ElementId>? entries, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var trash = TrashRepository.LoadTrash(connection);
            var roots = trash.Where(t => t.Id == t.DeletedRoot).Select(t => t.Id).ToHashSet();
            if (entries is not null && entries.FirstOrDefault(e => !roots.Contains(e)) is { IsNone: false } unknown)
            {
                return Result.Failure<int>(new DomainError(ErrorCodes.TrashEntryNotFound, $"Element {unknown} is not in the trash."));
            }

            var chosen = entries is null ? roots : roots.Intersect(entries).ToHashSet();
            var purged = trash.Where(t => chosen.Contains(t.DeletedRoot)).Select(t => t.Id).ToHashSet();
            var skeleton = TrashRepository.LoadSkeleton(connection);
            var orphans = new Dictionary<ElementId, ElementId>();
            foreach (var other in trash.Where(t => t.Id == t.DeletedRoot && !chosen.Contains(t.Id) && purged.Contains(t.ParentId)))
            {
                orphans[other.Id] = RootOfChain(skeleton, other.ParentId) ?? DefaultRootOf(other.Kind);
            }

            TrashRepository.Purge(connection, transaction, purged, orphans);
            CollectGarbage(connection);
            return Result.Success(purged.Count);
        }, cancellationToken);
}
