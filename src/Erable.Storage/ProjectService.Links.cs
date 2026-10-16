using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Links;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>A link type with the number of live links using it (F09.R3).</summary>
public sealed record LinkTypeInfo(ElementId Id, string Name, string Description, string? InverseName, int LinkCount);

/// <summary>One side of a link seen from a page: the label to show, the type, and the page at the other end.</summary>
public sealed record PageLink(ElementId LinkId, ElementId LinkTypeId, string TypeName, string Label, ElementId OtherId, string OtherName);

/// <summary>Typed links of a page: those it starts (outgoing) and those pointing to it (incoming, with the inverse label).</summary>
public sealed record PageLinks(IReadOnlyList<PageLink> Outgoing, IReadOnlyList<PageLink> Incoming);

public sealed partial class ProjectService
{
    // ─── Reads ──────────────────────────────────────────────────────────────

    /// <summary>Live link types by name, with their number of live links.</summary>
    public IReadOnlyList<LinkTypeInfo> ListLinkTypes()
    {
        using var connection = Project.Database.OpenConnection();
        var tree = Repository.LoadTree(connection);
        var counts = LiveLinks(connection, tree).GroupBy(l => l.LinkTypeId).ToDictionary(g => g.Key, g => g.Count());
        return [.. LinkRepository.LoadTypes(connection).Where(t => tree.Contains(t.Id))
            .Select(t => new LinkTypeInfo(t.Id, tree.Find(t.Id)!.Name, t.Description, t.InverseName, counts.GetValueOrDefault(t.Id)))
            .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(t => t.Id.Value)];
    }

    public LinkTypeInfo? GetLinkType(ElementId id) => ListLinkTypes().FirstOrDefault(t => t.Id == id);

    /// <summary>Typed links of a live page; null when it is not one.</summary>
    public PageLinks? GetPageLinks(ElementId pageId)
    {
        using var connection = Project.Database.OpenConnection();
        var tree = Repository.LoadTree(connection);
        if (tree.Find(pageId) is not { Kind: ElementKind.Page })
        {
            return null;
        }

        var types = LinkRepository.LoadTypes(connection).Where(t => tree.Contains(t.Id)).ToDictionary(t => t.Id);
        var links = LiveLinks(connection, tree);

        PageLink Side(LinkRecord link, bool outgoing)
        {
            var type = types[link.LinkTypeId];
            var typeName = tree.Find(type.Id)!.Name;
            var other = outgoing ? link.TargetId : link.SourceId;
            return new PageLink(link.Id, type.Id, typeName, outgoing ? typeName : type.InverseName ?? typeName, other, tree.Find(other)!.Name);
        }

        static IReadOnlyList<PageLink> Ordered(IEnumerable<PageLink> sides) =>
            [.. sides.OrderBy(l => l.Label, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.OtherName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(l => l.LinkId.Value)];

        return new PageLinks(
            Ordered(links.Where(l => l.SourceId == pageId).Select(l => Side(l, true))),
            Ordered(links.Where(l => l.TargetId == pageId).Select(l => Side(l, false))));
    }

    /// <summary>Links whose type and both pages are live: a link of a deleted type or page is hidden, and comes back with it.</summary>
    private static List<LinkRecord> LiveLinks(SqliteConnection connection, ElementTree tree)
    {
        var types = LinkRepository.LoadTypes(connection).Where(t => tree.Contains(t.Id)).Select(t => t.Id).ToHashSet();
        return [.. LinkRepository.LoadLinks(connection).Where(l =>
            types.Contains(l.LinkTypeId) && tree.Find(l.SourceId) is { Kind: ElementKind.Page } && tree.Find(l.TargetId) is { Kind: ElementKind.Page })];
    }

    // ─── Link types ─────────────────────────────────────────────────────────

    public Task<Result<Element>> CreateLinkTypeAsync(
        ElementId parentId, string name, int? index, string? description, string? inverseName, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var fields = LinkTypeRules.Normalize(description, inverseName);
            if (!fields.IsSuccess)
            {
                return Result.Failure<Element>(fields.Error);
            }

            var tree = Repository.LoadTree(connection);
            var id = NewUniqueId(connection, transaction);
            var created = tree.Create(id, ElementKind.LinkType, parentId, name, index);
            if (!created.IsSuccess)
            {
                return Result.Failure<Element>(created.Error);
            }

            var now = clock.GetUtcNow();
            var node = created.Value.Node;
            var element = new Element(id, ElementKind.LinkType, node.ParentId, node.Name, node.SortOrder, 1, now, author, now, author, null);
            ProjectRepository.InsertElement(connection, transaction, element);
            ProjectRepository.UpdateStructure(connection, transaction, created.Value.Changed.Where(n => n.Id != id), id, now, author);
            LinkRepository.InsertType(connection, transaction, id, fields.Value);
            InsertRevision(connection, transaction, element, null, author, now);
            return Result.Success(element);
        }, cancellationToken);

    /// <summary>Description and inverse label of a link type (F09.R1); its name goes through the tree.</summary>
    public Task<Result<Element>> UpdateLinkTypeAsync(
        ElementId id, string? description, string? inverseName, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var fields = LinkTypeRules.Normalize(description, inverseName);
            if (!fields.IsSuccess)
            {
                return Result.Failure<Element>(fields.Error);
            }

            var tree = Repository.LoadTree(connection);
            if (LinkRepository.GetType(connection, id) is null || !tree.Contains(id))
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.LinkTypeNotFound, $"Link type {id} does not exist."));
            }

            LinkRepository.InsertType(connection, transaction, id, fields.Value);
            var now = clock.GetUtcNow();
            ProjectRepository.Touch(connection, transaction, id, now, author);
            var element = Repository.GetElement(id, connection)!;
            InsertRevision(connection, transaction, element, null, author, now);
            return Result.Success(element);
        }, cancellationToken);

    // ─── Links ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Links two live pages with a type. A page cannot be linked to itself and the same (source, type, target) cannot be
    /// added twice (decision recorded in tasks/M2-07).
    /// </summary>
    public Task<Result<LinkRecord>> CreateLinkAsync(
        ElementId sourceId, ElementId linkTypeId, ElementId targetId, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            if (LinkRepository.GetType(connection, linkTypeId) is null || !tree.Contains(linkTypeId))
            {
                return Result.Failure<LinkRecord>(new DomainError(ErrorCodes.LinkTypeNotFound, $"Link type {linkTypeId} does not exist."));
            }

            if (tree.Find(sourceId) is not { Kind: ElementKind.Page } || tree.Find(targetId) is not { Kind: ElementKind.Page })
            {
                return Result.Failure<LinkRecord>(new DomainError(ErrorCodes.LinkPageInvalid, "Both ends of a link must be pages."));
            }

            if (sourceId == targetId)
            {
                return Result.Failure<LinkRecord>(new DomainError(ErrorCodes.LinkSelf, "A page cannot be linked to itself."));
            }

            if (LinkRepository.Exists(connection, sourceId, linkTypeId, targetId))
            {
                return Result.Failure<LinkRecord>(new DomainError(ErrorCodes.LinkDuplicate, "This link already exists."));
            }

            ElementId id;
            do
            {
                id = ElementId.NewRandom();
            }
            while (LinkRepository.LinkIdExists(connection, id));

            var link = new LinkRecord(id, sourceId, linkTypeId, targetId, clock.GetUtcNow(), author);
            LinkRepository.InsertLink(connection, transaction, link);
            return Result.Success(link);
        }, cancellationToken);

    public Task<Result<LinkRecord>> DeleteLinkAsync(ElementId id, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            if (LinkRepository.GetLink(connection, id) is not { } link)
            {
                return Result.Failure<LinkRecord>(new DomainError(ErrorCodes.LinkNotFound, $"Link {id} does not exist."));
            }

            LinkRepository.DeleteLink(connection, transaction, id);
            return Result.Success(link);
        }, cancellationToken);
}
