using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>An element of the trash, with the deletion it belongs to.</summary>
public sealed record TrashedElement(
    ElementId Id, ElementKind Kind, ElementId ParentId, string Name, ElementId DeletedRoot, DateTimeOffset DeletedAt, string DeletedBy, bool Orphan);

/// <summary>SQL of the trash (schema v3, M2-08): who deleted what, as which deletion, and permanent removal.</summary>
public static class TrashRepository
{
    /// <summary>Every element of the trash, one row each.</summary>
    public static IReadOnlyList<TrashedElement> LoadTrash(SqliteConnection connection) =>
        [.. connection.Query<(long Id, string Kind, long? Parent, string Name, long Root, string At, string? By, long Orphan)>(
            """
            SELECT id, kind, parent_id, name, deleted_root, deleted_at, deleted_by, deleted_orphan
            FROM elements WHERE deleted_at IS NOT NULL AND deleted_root IS NOT NULL
            ORDER BY deleted_at DESC, id
            """)
            .Select(r => new TrashedElement(
                ElementId.FromInt64(r.Id), StorageFormat.ParseKind(r.Kind), r.Parent is { } p ? ElementId.FromInt64(p) : ElementId.None, r.Name,
                ElementId.FromInt64(r.Root), StorageFormat.ParseTimestamp(r.At), r.By ?? string.Empty, r.Orphan != 0))];

    /// <summary>Marks elements as deleted together (one entry of the trash rooted at <paramref name="root"/>).</summary>
    public static void MarkDeleted(
        SqliteConnection connection, SqliteTransaction transaction, IEnumerable<ElementId> ids, ElementId root, DateTimeOffset at, string by) =>
        connection.Execute(
            "UPDATE elements SET deleted_at = @At, deleted_root = @Root, deleted_by = @By, deleted_orphan = 0 WHERE id IN (SELECT value FROM json_each(@Ids))",
            new { At = StorageFormat.Timestamp(at), Root = root.ToInt64(), By = by, Ids = StructureRepository.Json(ids) },
            transaction);

    /// <summary>Brings the elements of a deletion back, the entry itself under <paramref name="parent"/> when given.</summary>
    public static void MarkRestored(
        SqliteConnection connection, SqliteTransaction transaction, ElementId root, ElementId? parent, long? sortOrder)
    {
        connection.Execute(
            "UPDATE elements SET deleted_at = NULL, deleted_root = NULL, deleted_by = NULL, deleted_orphan = 0 WHERE deleted_root = @Root",
            new { Root = root.ToInt64() }, transaction);
        if (parent is { } p)
        {
            connection.Execute(
                "UPDATE elements SET parent_id = @Parent, sort_order = @Sort WHERE id = @Root",
                new { Parent = p.ToInt64(), Sort = sortOrder ?? 0, Root = root.ToInt64() }, transaction);
        }
    }

    /// <summary>Largest sort order among the live children of a parent (0 when none).</summary>
    public static long LastSortOrder(SqliteConnection connection, ElementId parent) =>
        connection.ExecuteScalar<long?>(
            "SELECT MAX(sort_order) FROM elements WHERE parent_id = @Parent AND deleted_at IS NULL", new { Parent = parent.ToInt64() }) ?? 0;

    /// <summary>Parent and name of every element, deleted ones included (original paths in the trash, chains to a root).</summary>
    public static Dictionary<ElementId, (ElementId Parent, string Name, ElementKind Kind)> LoadSkeleton(SqliteConnection connection) =>
        connection.Query<(long Id, long? Parent, string Name, string Kind)>("SELECT id, parent_id, name, kind FROM elements")
            .ToDictionary(
                r => ElementId.FromInt64(r.Id),
                r => (r.Parent is { } p ? ElementId.FromInt64(p) : ElementId.None, r.Name, StorageFormat.ParseKind(r.Kind)));

    /// <summary>
    /// Removes elements for good with everything that hangs from them (content, values, links, revisions, attachment
    /// rows…). Other deleted entries below them are first hung under <paramref name="orphanParents"/>.
    /// </summary>
    public static void Purge(
        SqliteConnection connection, SqliteTransaction transaction, IReadOnlyCollection<ElementId> ids,
        IReadOnlyDictionary<ElementId, ElementId> orphanParents)
    {
        foreach (var (orphan, parent) in orphanParents)
        {
            connection.Execute(
                "UPDATE elements SET parent_id = @Parent, deleted_orphan = 1 WHERE id = @Id",
                new { Parent = parent.ToInt64(), Id = orphan.ToInt64() }, transaction);
        }

        var parameters = new { Ids = StructureRepository.Json(ids) };
        const string In = "IN (SELECT value FROM json_each(@Ids))";
        string[] statements =
        [
            $"DELETE FROM resolved_values WHERE page_id {In} OR property_id {In}",
            $"DELETE FROM validation_issues WHERE owner_id {In} OR property_id {In}",
            $"DELETE FROM property_values WHERE owner_id {In} OR property_id {In}",
            $"DELETE FROM page_categories WHERE page_id {In} OR category_id {In}",
            $"UPDATE categories SET parent_category_id = NULL WHERE parent_category_id {In}",
            $"DELETE FROM links WHERE source_id {In} OR target_id {In} OR link_type_id {In}",
            $"DELETE FROM refs WHERE source_id {In}",
            $"DELETE FROM page_contents WHERE page_id {In}",
            $"DELETE FROM attachments WHERE id {In}",
            $"DELETE FROM categories WHERE id {In}",
            $"DELETE FROM property_defs WHERE id {In}",
            $"DELETE FROM link_types WHERE id {In}",
            $"DELETE FROM revisions WHERE element_id {In}",
            $"DELETE FROM user_activity WHERE element_id {In}",
            $"DELETE FROM search_fts WHERE rowid {In}",
            $"DELETE FROM elements WHERE id {In}",
        ];
        foreach (var statement in statements)
        {
            connection.Execute(statement, parameters, transaction);
        }
    }
}
