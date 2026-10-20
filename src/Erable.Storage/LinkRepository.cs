using Dapper;
using Erable.Core;
using Erable.Core.Links;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

/// <summary>Stored fields of a link type (its name is the element name).</summary>
public sealed record LinkTypeRecord(ElementId Id, string Description, string? InverseName);

/// <summary>A link between two pages.</summary>
public sealed record LinkRecord(ElementId Id, ElementId SourceId, ElementId LinkTypeId, ElementId TargetId, DateTimeOffset CreatedAt, string CreatedBy);

/// <summary>SQL of link types and links (schema v2). Same conventions as <see cref="StructureRepository"/>.</summary>
public static class LinkRepository
{
    public static IReadOnlyList<LinkTypeRecord> LoadTypes(SqliteConnection connection) =>
        [.. connection.Query<(long Id, string Description, string? Inverse)>("SELECT id, description, inverse_name FROM link_types")
            .Select(r => new LinkTypeRecord(ElementId.FromInt64(r.Id), r.Description, r.Inverse))];

    public static LinkTypeRecord? GetType(SqliteConnection connection, ElementId id) =>
        connection.QuerySingleOrDefault<(long Id, string Description, string? Inverse)?>(
            "SELECT id, description, inverse_name FROM link_types WHERE id = @Id", new { Id = id.ToInt64() }) is { } r
            ? new LinkTypeRecord(id, r.Description, r.Inverse)
            : null;

    public static void UpsertType(SqliteConnection connection, SqliteTransaction transaction, LinkTypeRecord type)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(type);
        connection.Execute(
            """
            INSERT INTO link_types (id, description, inverse_name) VALUES (@Id, @Description, @Inverse)
            ON CONFLICT (id) DO UPDATE SET description = excluded.description, inverse_name = excluded.inverse_name
            """,
            new { Id = type.Id.ToInt64(), type.Description, Inverse = type.InverseName },
            transaction);
    }

    public static void InsertType(SqliteConnection connection, SqliteTransaction transaction, ElementId id, LinkTypeFields fields) =>
        UpsertType(connection, transaction, new LinkTypeRecord(id, fields.Description, fields.InverseName));

    /// <summary>Every link, deleted ends or types included (the caller filters with the live tree).</summary>
    public static IReadOnlyList<LinkRecord> LoadLinks(SqliteConnection connection) =>
        [.. connection.Query<(long Id, long Source, long Type, long Target, string CreatedAt, string CreatedBy)>(
            "SELECT id, source_id, link_type_id, target_id, created_at, created_by FROM links ORDER BY created_at, id")
            .Select(Map)];

    public static LinkRecord? GetLink(SqliteConnection connection, ElementId id) =>
        connection.QuerySingleOrDefault<(long Id, long Source, long Type, long Target, string CreatedAt, string CreatedBy)?>(
            "SELECT id, source_id, link_type_id, target_id, created_at, created_by FROM links WHERE id = @Id", new { Id = id.ToInt64() }) is { } r
            ? Map(r)
            : null;

    public static bool LinkIdExists(SqliteConnection connection, ElementId id) =>
        connection.ExecuteScalar<long>("SELECT COUNT(*) FROM links WHERE id = @Id", new { Id = id.ToInt64() }) > 0;

    public static bool Exists(SqliteConnection connection, ElementId source, ElementId type, ElementId target) =>
        connection.ExecuteScalar<long>(
            "SELECT COUNT(*) FROM links WHERE source_id = @Source AND link_type_id = @Type AND target_id = @Target",
            new { Source = source.ToInt64(), Type = type.ToInt64(), Target = target.ToInt64() }) > 0;

    public static void InsertLink(SqliteConnection connection, SqliteTransaction transaction, LinkRecord link)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(link);
        connection.Execute(
            """
            INSERT INTO links (id, source_id, link_type_id, target_id, created_at, created_by)
            VALUES (@Id, @Source, @Type, @Target, @CreatedAt, @CreatedBy)
            """,
            new
            {
                Id = link.Id.ToInt64(),
                Source = link.SourceId.ToInt64(),
                Type = link.LinkTypeId.ToInt64(),
                Target = link.TargetId.ToInt64(),
                CreatedAt = StorageFormat.Timestamp(link.CreatedAt),
                link.CreatedBy,
            },
            transaction);
    }

    public static void DeleteLink(SqliteConnection connection, SqliteTransaction transaction, ElementId id) =>
        connection.Execute("DELETE FROM links WHERE id = @Id", new { Id = id.ToInt64() }, transaction);

    private static LinkRecord Map((long Id, long Source, long Type, long Target, string CreatedAt, string CreatedBy) r) => new(
        ElementId.FromInt64(r.Id), ElementId.FromInt64(r.Source), ElementId.FromInt64(r.Type), ElementId.FromInt64(r.Target),
        StorageFormat.ParseTimestamp(r.CreatedAt), r.CreatedBy);
}
