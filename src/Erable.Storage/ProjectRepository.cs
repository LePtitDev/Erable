using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>Markdown of a page with the element revision it belongs to.</summary>
public sealed record StoredPageContent(ElementId PageId, string Markdown, string PlainText, long Revision);

/// <summary>One row of the derived <c>refs</c> index.</summary>
public sealed record StoredReference(ElementId SourceId, ElementId TargetId, string? Anchor, bool IsEmbed, int Position);

/// <summary>One saved state of an element: who, when, and the whole state (F13.R1).</summary>
public sealed record StoredRevision(long Revision, string Author, DateTimeOffset At, RevisionSnapshot Snapshot);

/// <summary>Attachment metadata (the content is the blob named by <see cref="Sha256"/>).</summary>
public sealed record AttachmentRecord(ElementId Id, ElementId PageId, string FileName, string Mime, long Size, string Sha256);

/// <summary>
/// Explicit SQL over a project database (Dapper, bound parameters only). Read methods open their own connection;
/// write methods take the connection and transaction of <see cref="ProjectDatabase.WriteAsync{T}"/>, so that a
/// business operation is one transaction.
/// </summary>
public sealed class ProjectRepository(ProjectDatabase database)
{
    private const string ElementColumns = """
        id AS Id, kind AS Kind, parent_id AS ParentId, name AS Name, sort_order AS SortOrder, revision AS Revision,
        created_at AS CreatedAt, created_by AS CreatedBy, updated_at AS UpdatedAt, updated_by AS UpdatedBy, deleted_at AS DeletedAt
        """;

    public ProjectDatabase Database { get; } = database;

    // ─── Elements ───────────────────────────────────────────────────────────

    /// <summary>Structure of the live (not deleted) elements, attachments included.</summary>
    public IReadOnlyList<TreeNode> LoadTreeNodes(SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return ReadTreeNodes(connection ?? owned!);
    }

    /// <summary>Structure of the live (not deleted) elements, on a given connection.</summary>
    public static IReadOnlyList<TreeNode> ReadTreeNodes(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var rows = connection.Query<TreeRow>(
            "SELECT id AS Id, kind AS Kind, parent_id AS ParentId, name AS Name, sort_order AS SortOrder FROM elements WHERE deleted_at IS NULL");
        return [.. rows.Select(r => r.ToNode())];
    }

    public ElementTree LoadTree(SqliteConnection? connection = null) => ElementTree.Load(LoadTreeNodes(connection));

    /// <summary>The element, deleted or not; null when the ID is unknown.</summary>
    public Element? GetElement(ElementId id, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return (connection ?? owned!).QuerySingleOrDefault<ElementRow>(
            $"SELECT {ElementColumns} FROM elements WHERE id = @Id", new { Id = id.ToInt64() })?.ToElement();
    }

    /// <summary>Several elements in one query (deleted ones included); unknown IDs are simply absent.</summary>
    public IReadOnlyList<Element> GetElements(IReadOnlyCollection<ElementId> ids, SqliteConnection? connection = null)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        using var owned = connection is null ? Database.OpenConnection() : null;
        // A JSON array parameter instead of N parameters: no limit on the number of IDs, one prepared statement.
        var json = "[" + string.Join(',', ids.Select(i => i.ToInt64())) + "]";
        return [.. (connection ?? owned!).Query<ElementRow>(
            $"SELECT {ElementColumns} FROM elements WHERE id IN (SELECT value FROM json_each(@Ids))", new { Ids = json })
            .Select(r => r.ToElement())];
    }

    public static void InsertElement(SqliteConnection connection, SqliteTransaction transaction, Element element)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(element);
        connection.Execute(
            """
            INSERT INTO elements (id, kind, parent_id, name, sort_order, revision, created_at, created_by, updated_at, updated_by, deleted_at)
            VALUES (@Id, @Kind, @ParentId, @Name, @SortOrder, @Revision, @CreatedAt, @CreatedBy, @UpdatedAt, @UpdatedBy, @DeletedAt)
            """,
            ElementRow.From(element),
            transaction);
    }

    /// <summary>
    /// Writes the structure of nodes changed by a tree operation. <paramref name="subject"/> is the element the user
    /// acted on: its revision and modification stamp advance; renumbered siblings only get their new order.
    /// </summary>
    public static void UpdateStructure(
        SqliteConnection connection, SqliteTransaction transaction, IEnumerable<TreeNode> changed, ElementId subject,
        DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(changed);
        foreach (var node in changed)
        {
            var touched = node.Id == subject;
            connection.Execute(
                touched
                    ? """
                      UPDATE elements SET parent_id = @ParentId, name = @Name, sort_order = @SortOrder,
                          revision = revision + 1, updated_at = @At, updated_by = @By
                      WHERE id = @Id
                      """
                    : "UPDATE elements SET parent_id = @ParentId, name = @Name, sort_order = @SortOrder WHERE id = @Id",
                new
                {
                    Id = node.Id.ToInt64(),
                    ParentId = node.ParentId.IsNone ? (long?)null : node.ParentId.ToInt64(),
                    node.Name,
                    node.SortOrder,
                    At = StorageFormat.Timestamp(at),
                    By = by,
                },
                transaction);
        }
    }

    /// <summary>Advances the revision and modification stamp of an element; returns the new revision.</summary>
    public static long Touch(SqliteConnection connection, SqliteTransaction transaction, ElementId id, DateTimeOffset at, string by)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return connection.ExecuteScalar<long>(
            "UPDATE elements SET revision = revision + 1, updated_at = @At, updated_by = @By WHERE id = @Id RETURNING revision",
            new { Id = id.ToInt64(), At = StorageFormat.Timestamp(at), By = by },
            transaction);
    }

    // ─── Pages ──────────────────────────────────────────────────────────────

    public StoredPageContent? GetPageContent(ElementId pageId, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        var row = (connection ?? owned!).QuerySingleOrDefault<(string Markdown, string PlainText, long Revision)>(
            """
            SELECT c.markdown, c.plain_text, e.revision
            FROM page_contents c JOIN elements e ON e.id = c.page_id
            WHERE c.page_id = @Id
            """,
            new { Id = pageId.ToInt64() });
        return row.Markdown is null ? null : new StoredPageContent(pageId, row.Markdown, row.PlainText, row.Revision);
    }

    public static void UpsertPageContent(
        SqliteConnection connection, SqliteTransaction transaction, ElementId pageId, string markdown, string plainText)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.Execute(
            """
            INSERT INTO page_contents (page_id, markdown, plain_text) VALUES (@Id, @Markdown, @PlainText)
            ON CONFLICT (page_id) DO UPDATE SET markdown = excluded.markdown, plain_text = excluded.plain_text
            """,
            new { Id = pageId.ToInt64(), Markdown = markdown, PlainText = plainText },
            transaction);
    }

    // ─── References ─────────────────────────────────────────────────────────

    /// <summary>Replaces the outgoing references of a page (derived index, docs/architecture.md §5.6).</summary>
    public static void ReplaceReferences(
        SqliteConnection connection, SqliteTransaction transaction, ElementId sourceId, IEnumerable<StoredReference> references)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.Execute("DELETE FROM refs WHERE source_id = @Id", new { Id = sourceId.ToInt64() }, transaction);
        connection.Execute(
            "INSERT INTO refs (source_id, target_id, anchor, is_embed, position) VALUES (@SourceId, @TargetId, @Anchor, @IsEmbed, @Position)",
            references.Select(r => new
            {
                SourceId = r.SourceId.ToInt64(),
                TargetId = r.TargetId.ToInt64(),
                r.Anchor,
                IsEmbed = r.IsEmbed ? 1 : 0,
                r.Position,
            }),
            transaction);
    }

    public IReadOnlyList<StoredReference> GetOutgoingReferences(ElementId sourceId, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<RefRow>(
            "SELECT source_id AS SourceId, target_id AS TargetId, anchor AS Anchor, is_embed AS IsEmbed, position AS Position FROM refs WHERE source_id = @Id ORDER BY position",
            new { Id = sourceId.ToInt64() }).Select(r => r.ToReference())];
    }

    /// <summary>References pointing to <paramref name="targetId"/> from live elements.</summary>
    public IReadOnlyList<StoredReference> GetIncomingReferences(ElementId targetId, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<RefRow>(
            """
            SELECT r.source_id AS SourceId, r.target_id AS TargetId, r.anchor AS Anchor, r.is_embed AS IsEmbed, r.position AS Position
            FROM refs r JOIN elements e ON e.id = r.source_id
            WHERE r.target_id = @Id AND e.deleted_at IS NULL
            ORDER BY r.source_id, r.position
            """,
            new { Id = targetId.ToInt64() }).Select(r => r.ToReference())];
    }

    // ─── Revisions ──────────────────────────────────────────────────────────

    public static void InsertRevision(
        SqliteConnection connection, SqliteTransaction transaction, ElementId id, long revision, string author,
        DateTimeOffset at, byte[] snapshot, string? blobSha256 = null)
    {
        ArgumentNullException.ThrowIfNull(connection);
        connection.Execute(
            "INSERT INTO revisions (element_id, revision, author, at, snapshot, blob_sha256) VALUES (@Id, @Revision, @Author, @At, @Snapshot, @Blob)",
            new { Id = id.ToInt64(), Revision = revision, Author = author, At = StorageFormat.Timestamp(at), Snapshot = snapshot, Blob = blobSha256 },
            transaction);
    }

    /// <summary>The revisions of an element, oldest first, with their decoded snapshot.</summary>
    public IReadOnlyList<StoredRevision> LoadRevisions(ElementId id, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<(long Revision, string Author, string At, byte[] Snapshot)>(
                "SELECT revision, author, at, snapshot FROM revisions WHERE element_id = @Id ORDER BY revision",
                new { Id = id.ToInt64() })
            .Select(r => new StoredRevision(r.Revision, r.Author, StorageFormat.ParseTimestamp(r.At), RevisionSnapshot.Deserialize(r.Snapshot)))];
    }

    /// <summary>One revision of an element, without reading the others; null when it does not exist.</summary>
    public StoredRevision? LoadRevision(ElementId id, long revision, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        var row = (connection ?? owned!).QuerySingleOrDefault<(long Revision, string Author, string At, byte[] Snapshot)?>(
            "SELECT revision, author, at, snapshot FROM revisions WHERE element_id = @Id AND revision = @Revision",
            new { Id = id.ToInt64(), Revision = revision });
        return row is { } r
            ? new StoredRevision(r.Revision, r.Author, StorageFormat.ParseTimestamp(r.At), RevisionSnapshot.Deserialize(r.Snapshot))
            : null;
    }

    /// <summary>The authors of the revisions saved after <paramref name="revision"/>, oldest first.</summary>
    public IReadOnlyList<string> RevisionAuthorsAfter(ElementId id, long revision, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<string>(
            "SELECT author FROM revisions WHERE element_id = @Id AND revision > @Revision ORDER BY revision",
            new { Id = id.ToInt64(), Revision = revision })];
    }

    public int CountRevisions(ElementId id)
    {
        using var connection = Database.OpenConnection();
        return connection.ExecuteScalar<int>("SELECT count(*) FROM revisions WHERE element_id = @Id", new { Id = id.ToInt64() });
    }

    // ─── Attachments ────────────────────────────────────────────────────────

    public static void InsertAttachment(SqliteConnection connection, SqliteTransaction transaction, AttachmentRecord attachment)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(attachment);
        connection.Execute(
            "INSERT INTO attachments (id, file_name, mime, size, sha256) VALUES (@Id, @FileName, @Mime, @Size, @Sha256)",
            new { Id = attachment.Id.ToInt64(), attachment.FileName, attachment.Mime, attachment.Size, attachment.Sha256 },
            transaction);
    }

    public AttachmentRecord? GetAttachment(ElementId id, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return (connection ?? owned!).QuerySingleOrDefault<AttachmentRow>(
            """
            SELECT a.id AS Id, e.parent_id AS PageId, a.file_name AS FileName, a.mime AS Mime, a.size AS Size, a.sha256 AS Sha256
            FROM attachments a JOIN elements e ON e.id = a.id
            WHERE a.id = @Id AND e.deleted_at IS NULL
            """,
            new { Id = id.ToInt64() })?.ToRecord();
    }

    /// <summary>Several attachments in one query, deleted ones included; unknown IDs are absent.</summary>
    public IReadOnlyList<AttachmentRecord> GetAttachments(IReadOnlyCollection<ElementId> ids, SqliteConnection? connection = null)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        using var owned = connection is null ? Database.OpenConnection() : null;
        var json = "[" + string.Join(',', ids.Select(i => i.ToInt64())) + "]";
        return [.. (connection ?? owned!).Query<AttachmentRow>(
            """
            SELECT a.id AS Id, e.parent_id AS PageId, a.file_name AS FileName, a.mime AS Mime, a.size AS Size, a.sha256 AS Sha256
            FROM attachments a JOIN elements e ON e.id = a.id
            WHERE a.id IN (SELECT value FROM json_each(@Ids))
            """,
            new { Ids = json }).Select(r => r.ToRecord())];
    }

    /// <summary>Every live attachment of the project (insert palette): those of a deleted page are excluded.</summary>
    public IReadOnlyList<AttachmentRecord> GetAllAttachments(SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<AttachmentRow>(
            """
            SELECT a.id AS Id, e.parent_id AS PageId, a.file_name AS FileName, a.mime AS Mime, a.size AS Size, a.sha256 AS Sha256
            FROM attachments a JOIN elements e ON e.id = a.id JOIN elements p ON p.id = e.parent_id
            WHERE e.deleted_at IS NULL AND p.deleted_at IS NULL
            ORDER BY e.parent_id, e.sort_order, a.id
            """).Select(r => r.ToRecord())];
    }

    /// <summary>Live attachments of a page, in their order.</summary>
    public IReadOnlyList<AttachmentRecord> GetAttachments(ElementId pageId, SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return [.. (connection ?? owned!).Query<AttachmentRow>(
            """
            SELECT a.id AS Id, e.parent_id AS PageId, a.file_name AS FileName, a.mime AS Mime, a.size AS Size, a.sha256 AS Sha256
            FROM attachments a JOIN elements e ON e.id = a.id
            WHERE e.parent_id = @Id AND e.deleted_at IS NULL
            ORDER BY e.sort_order, a.id
            """,
            new { Id = pageId.ToInt64() }).Select(r => r.ToRecord())];
    }

    /// <summary>Every blob hash still referenced by an attachment (deleted or not) or by a revision.</summary>
    public IReadOnlySet<string> GetReferencedBlobs(SqliteConnection? connection = null)
    {
        using var owned = connection is null ? Database.OpenConnection() : null;
        return (connection ?? owned!).Query<string>(
            "SELECT sha256 FROM attachments UNION SELECT blob_sha256 FROM revisions WHERE blob_sha256 IS NOT NULL")
            .ToHashSet(StringComparer.Ordinal);
    }

    // ─── Rows ───────────────────────────────────────────────────────────────

    private sealed record TreeRow(long Id, string Kind, long? ParentId, string Name, long SortOrder)
    {
        public TreeNode ToNode() => new(
            ElementId.FromInt64(Id), StorageFormat.ParseKind(Kind),
            ParentId is { } p ? ElementId.FromInt64(p) : ElementId.None, Name, SortOrder);
    }

    private sealed record ElementRow(
        long Id, string Kind, long? ParentId, string Name, long SortOrder, long Revision,
        string CreatedAt, string CreatedBy, string UpdatedAt, string UpdatedBy, string? DeletedAt)
    {
        public Element ToElement() => new(
            ElementId.FromInt64(Id), StorageFormat.ParseKind(Kind),
            ParentId is { } p ? ElementId.FromInt64(p) : ElementId.None, Name, SortOrder, Revision,
            StorageFormat.ParseTimestamp(CreatedAt), CreatedBy, StorageFormat.ParseTimestamp(UpdatedAt), UpdatedBy,
            StorageFormat.ParseTimestampOrNull(DeletedAt));

        public static ElementRow From(Element e) => new(
            e.Id.ToInt64(), StorageFormat.Kind(e.Kind), e.ParentId.IsNone ? null : e.ParentId.ToInt64(), e.Name, e.SortOrder,
            e.Revision, StorageFormat.Timestamp(e.CreatedAt), e.CreatedBy, StorageFormat.Timestamp(e.UpdatedAt), e.UpdatedBy,
            e.DeletedAt is { } d ? StorageFormat.Timestamp(d) : null);
    }

    private sealed record RefRow(long SourceId, long TargetId, string? Anchor, long IsEmbed, long Position)
    {
        public StoredReference ToReference() =>
            new(ElementId.FromInt64(SourceId), ElementId.FromInt64(TargetId), Anchor, IsEmbed != 0, (int)Position);
    }

    private sealed record AttachmentRow(long Id, long PageId, string FileName, string Mime, long Size, string Sha256)
    {
        public AttachmentRecord ToRecord() =>
            new(ElementId.FromInt64(Id), ElementId.FromInt64(PageId), FileName, Mime, Size, Sha256);
    }
}
