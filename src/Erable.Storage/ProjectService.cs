using Dapper;
using Erable.Core;
using Erable.Core.Attachments;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>A page saved: its new revision and modification stamp.</summary>
public sealed record PageSaved(ElementId PageId, long Revision, DateTimeOffset UpdatedAt, string UpdatedBy);

/// <summary>Where an element lives: ancestors from the root (included) down to the parent.</summary>
public sealed record ElementWithPath(Element Element, IReadOnlyList<TreeNode> Path);

/// <summary>Resolution state of a referenced ID (F04.R1–R2).</summary>
public enum ResolvedState
{
    Existing,
    Deleted,
    Unknown,
}

public sealed record ResolvedElement(ElementId Id, ResolvedState State, Element? Element, IReadOnlyList<TreeNode> Path);

/// <summary>Incoming references of an element, grouped by source.</summary>
public sealed record Backlink(Element Source, IReadOnlyList<StoredReference> References);

/// <summary>
/// Application operations on an open project: each write is one transaction that applies the Core rules, updates the
/// derived indexes (references) and records a revision (F13.R1). Reads never block writes (WAL).
/// </summary>
public sealed class ProjectService(OpenedProject project, TimeProvider clock)
{
    /// <summary>Upper bound of IDs per <see cref="Resolve"/> call.</summary>
    public const int MaxResolve = 500;

    public static readonly DomainError RevisionConflict = new("page.revisionConflict", "The page changed since it was loaded.");

    public OpenedProject Project { get; } = project;

    private ProjectRepository Repository => Project.Repository;

    // ─── Reads ──────────────────────────────────────────────────────────────

    public ElementTree LoadTree() => Repository.LoadTree();

    public ElementWithPath? GetElement(ElementId id)
    {
        using var connection = Project.Database.OpenConnection();
        var element = Repository.GetElement(id, connection);
        if (element is null)
        {
            return null;
        }

        var tree = Repository.LoadTree(connection);
        return new ElementWithPath(element, PathOf(tree, element));
    }

    /// <summary>Names, kinds and paths of N IDs in one call (pills of the editor, rendering).</summary>
    public IReadOnlyList<ResolvedElement> Resolve(IReadOnlyCollection<ElementId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        using var connection = Project.Database.OpenConnection();
        var found = Repository.GetElements(ids, connection).ToDictionary(e => e.Id);
        var tree = Repository.LoadTree(connection);
        return [.. ids.Distinct().Select(id => found.TryGetValue(id, out var element)
            ? new ResolvedElement(id, IsDeleted(tree, element) ? ResolvedState.Deleted : ResolvedState.Existing, element, PathOf(tree, element))
            : new ResolvedElement(id, ResolvedState.Unknown, null, []))];
    }

    /// <summary>Live elements whose content references <paramref name="id"/>.</summary>
    public IReadOnlyList<Backlink> GetBacklinks(ElementId id)
    {
        using var connection = Project.Database.OpenConnection();
        var references = Repository.GetIncomingReferences(id, connection);
        var sources = Repository.GetElements([.. references.Select(r => r.SourceId).Distinct()], connection).ToDictionary(e => e.Id);
        return [.. references.GroupBy(r => r.SourceId)
            .Where(g => sources.ContainsKey(g.Key))
            .Select(g => new Backlink(sources[g.Key], [.. g]))
            .OrderBy(b => b.Source.Name, StringComparer.CurrentCultureIgnoreCase)];
    }

    // ─── Writes ─────────────────────────────────────────────────────────────

    /// <summary>Creates a folder or a page (other kinds arrive with M2) at <paramref name="index"/> among its siblings.</summary>
    public Task<Result<Element>> CreateElementAsync(
        ElementKind kind, ElementId parentId, string name, int? index, string author, string? markdown = null,
        CancellationToken cancellationToken = default)
    {
        if (kind is not (ElementKind.Folder or ElementKind.Page))
        {
            return Task.FromResult(Result.Failure<Element>(new DomainError(ErrorCodes.KindNotCreatable, $"{kind} cannot be created yet.")));
        }

        return Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            var id = NewUniqueId(connection, transaction);
            var created = tree.Create(id, kind, parentId, name, index);
            if (!created.IsSuccess)
            {
                return Result.Failure<Element>(created.Error);
            }

            var now = clock.GetUtcNow();
            var node = created.Value.Node;
            var element = new Element(id, kind, node.ParentId, node.Name, node.SortOrder, 1, now, author, now, author, null);
            ProjectRepository.InsertElement(connection, transaction, element);
            ProjectRepository.UpdateStructure(connection, transaction, created.Value.Changed.Where(n => n.Id != id), id, now, author);

            string? content = null;
            if (kind == ElementKind.Page)
            {
                content = markdown ?? string.Empty;
                WriteContent(connection, transaction, id, content);
            }

            InsertRevision(connection, transaction, element, content, author, now);
            return Result.Success(element);
        }, cancellationToken);
    }

    /// <summary>Renames and/or moves an element (F02); a refused move changes nothing.</summary>
    public Task<Result<Element>> UpdateElementAsync(
        ElementId id, string? name, ElementId? parentId, int? index, string author, CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            var node = tree.Find(id);
            if (node is null)
            {
                return Result.Failure<Element>(new DomainError(ErrorCodes.ElementNotFound, $"Element {id} does not exist."));
            }

            var changed = new Dictionary<ElementId, TreeNode>();
            if (name is not null && name != node.Name)
            {
                var renamed = tree.Rename(id, name);
                if (!renamed.IsSuccess)
                {
                    return Result.Failure<Element>(renamed.Error);
                }

                Collect(changed, renamed.Value);
            }

            if (parentId is not null || index is not null)
            {
                var moved = tree.Move(id, parentId ?? node.ParentId, index);
                if (!moved.IsSuccess)
                {
                    return Result.Failure<Element>(moved.Error);
                }

                Collect(changed, moved.Value);
            }

            if (changed.Count == 0)
            {
                return Result.Success(Repository.GetElement(id, connection)!);
            }

            var now = clock.GetUtcNow();
            ProjectRepository.UpdateStructure(connection, transaction, changed.Values, id, now, author);
            var element = Repository.GetElement(id, connection)!;
            InsertRevision(connection, transaction, element, Repository.GetPageContent(id, connection)?.Markdown, author, now);
            return Result.Success(element);
        }, cancellationToken);

    /// <summary>
    /// Saves the Markdown of a page if it is still at <paramref name="expectedRevision"/> (optimistic concurrency;
    /// the three-way merge arrives with M4-05). Content, plain text, references and revision: one transaction.
    /// </summary>
    public Task<Result<PageSaved>> SavePageContentAsync(
        ElementId pageId, string markdown, long expectedRevision, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            var element = Repository.GetElement(pageId, connection);
            if (element is not { Kind: ElementKind.Page, DeletedAt: null })
            {
                return Result.Failure<PageSaved>(new DomainError(ErrorCodes.ElementNotFound, $"Page {pageId} does not exist."));
            }

            if (element.Revision != expectedRevision)
            {
                return Result.Failure<PageSaved>(RevisionConflict);
            }

            var now = clock.GetUtcNow();
            WriteContent(connection, transaction, pageId, markdown);
            var revision = ProjectRepository.Touch(connection, transaction, pageId, now, author);
            var saved = element with { Revision = revision, UpdatedAt = now, UpdatedBy = author };
            InsertRevision(connection, transaction, saved, markdown, author, now);
            return Result.Success(new PageSaved(pageId, revision, now, author));
        }, cancellationToken);
    }

    // ─── Attachments ────────────────────────────────────────────────────────

    /// <summary>
    /// Attaches a file to a page (F06): the content is streamed into the blob store first (temporary file, fsync, atomic
    /// rename), then the element and its metadata are inserted in one transaction. A refused or interrupted upload
    /// leaves no attachment; at worst an unreferenced blob, removed by <see cref="CollectGarbageAsync"/>.
    /// </summary>
    public async Task<Result<AttachmentRecord>> AddAttachmentAsync(
        ElementId pageId, string? fileName, Stream content, long maxSize, string author, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var name = AttachmentFileName.Normalize(fileName);
        if (!name.IsSuccess)
        {
            return name.Error;
        }

        if (Repository.GetElement(pageId) is not { Kind: ElementKind.Page, DeletedAt: null })
        {
            return new DomainError(ErrorCodes.ElementNotFound, $"Page {pageId} does not exist.");
        }

        using var blob = await Project.Blobs.WriteAsync(content, maxSize, cancellationToken).ConfigureAwait(false);
        if (blob is null)
        {
            return new DomainError(ErrorCodes.AttachmentTooLarge, $"The file exceeds the limit of {maxSize} bytes.");
        }

        var mime = MediaTypes.Detect(blob.Head.Span, name.Value);
        return await Project.Database.WriteAsync((connection, transaction) =>
        {
            var tree = Repository.LoadTree(connection);
            var id = NewUniqueId(connection, transaction);
            var created = tree.Create(id, ElementKind.Attachment, pageId, name.Value);
            if (!created.IsSuccess)
            {
                return Result.Failure<AttachmentRecord>(created.Error);
            }

            var now = clock.GetUtcNow();
            var node = created.Value.Node;
            var element = new Element(id, ElementKind.Attachment, pageId, node.Name, node.SortOrder, 1, now, author, now, author, null);
            var attachment = new AttachmentRecord(id, pageId, node.Name, mime, blob.Size, blob.Sha256);
            ProjectRepository.InsertElement(connection, transaction, element);
            ProjectRepository.InsertAttachment(connection, transaction, attachment);
            var snapshot = new RevisionSnapshot(id, ElementKind.Attachment, pageId, node.Name, null,
                new AttachmentSnapshot(node.Name, mime, blob.Size, blob.Sha256));
            ProjectRepository.InsertRevision(connection, transaction, id, 1, author, now, snapshot.Serialize(), blob.Sha256);
            return Result.Success(attachment);
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A live attachment and a read stream on its content; null when either is missing.</summary>
    public (AttachmentRecord Attachment, Stream Content)? OpenAttachment(ElementId id)
    {
        if (Repository.GetAttachment(id) is not { } attachment || Project.Blobs.OpenRead(attachment.Sha256) is not { } stream)
        {
            return null;
        }

        return (attachment, stream);
    }

    /// <summary>
    /// Deletes the blobs that neither an attachment (in the trash or not) nor a revision references (docs/architecture.md
    /// §7.3). Runs under the write gate; blobs of uploads not yet committed are leased and kept.
    /// </summary>
    public Task<IReadOnlyList<string>> CollectGarbageAsync(CancellationToken cancellationToken = default) =>
        Project.Database.WriteAsync<IReadOnlyList<string>>((connection, _) => CollectGarbage(connection), cancellationToken);

    private List<string> CollectGarbage(SqliteConnection connection)
    {
        var referenced = Repository.GetReferencedBlobs(connection);
        var deleted = new List<string>();
        foreach (var sha256 in Project.Blobs.List())
        {
            if (!referenced.Contains(sha256) && !Project.Blobs.IsLeased(sha256))
            {
                Project.Blobs.Delete(sha256);
                deleted.Add(sha256);
            }
        }

        return deleted;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Content, plain text and reference index of a page, from one analysis of its Markdown.</summary>
    private static void WriteContent(SqliteConnection connection, SqliteTransaction transaction, ElementId pageId, string markdown)
    {
        var analysis = ErableMarkdown.Analyze(markdown);
        ProjectRepository.UpsertPageContent(connection, transaction, pageId, markdown, analysis.PlainText);
        ProjectRepository.ReplaceReferences(connection, transaction, pageId,
            analysis.References.Select(r => new StoredReference(pageId, r.Id, r.Anchor, r.IsEmbed, r.Start)));
    }

    private static void InsertRevision(
        SqliteConnection connection, SqliteTransaction transaction, Element element, string? markdown, string author, DateTimeOffset at)
    {
        var snapshot = new RevisionSnapshot(element.Id, element.Kind, element.ParentId, element.Name, markdown, null);
        ProjectRepository.InsertRevision(connection, transaction, element.Id, element.Revision, author, at, snapshot.Serialize());
    }

    /// <summary>A random ID never used in this project, deleted elements included (docs/architecture.md §4).</summary>
    private static ElementId NewUniqueId(SqliteConnection connection, SqliteTransaction transaction)
    {
        while (true)
        {
            var id = ElementId.NewRandom();
            if (connection.ExecuteScalar<long>("SELECT count(*) FROM elements WHERE id = @Id", new { Id = id.ToInt64() }, transaction) == 0)
            {
                return id;
            }
        }
    }

    private static void Collect(Dictionary<ElementId, TreeNode> changed, TreeChange change)
    {
        foreach (var node in change.Changed)
        {
            changed[node.Id] = node;
        }
    }

    /// <summary>Deleted, or under a deleted ancestor (absent from the live tree).</summary>
    private static bool IsDeleted(ElementTree tree, Element element) => element.DeletedAt is not null || !tree.Contains(element.Id);

    private static IReadOnlyList<TreeNode> PathOf(ElementTree tree, Element element) =>
        tree.Contains(element.Id) ? [.. tree.Ancestors(element.Id).Reverse()] : [];
}
