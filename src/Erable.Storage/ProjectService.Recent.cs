using Dapper;
using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Storage;

/// <summary>An element a user viewed, and when.</summary>
public sealed record ViewedElement(Element Element, DateTimeOffset ViewedAt);

/// <summary>What a user was working on (F11): their own views, and the latest changes of everybody.</summary>
public sealed record RecentActivity(IReadOnlyList<ViewedElement> Viewed, IReadOnlyList<Element> Edited);

public sealed partial class ProjectService
{
    /// <summary>Upper bound of items per list of <see cref="GetRecent"/>.</summary>
    public const int MaxRecent = 50;

    // ─── Recently viewed and edited (F11) ────────────────────────────

    /// <summary>
    /// Counts a view of <paramref name="id"/> by <paramref name="userId"/> (F11.R1: the caller decides what counts as a view;
    /// the SPA waits two seconds). Roots, attachments and elements in the trash are not recorded.
    /// </summary>
    public Task<Result<DateTimeOffset>> RecordViewAsync(string userId, ElementId id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        return Project.Database.WriteAsync((connection, transaction) =>
        {
            var element = Repository.GetElement(id, connection);
            if (element is not { DeletedAt: null } || element.Kind is ElementKind.Root or ElementKind.Attachment)
            {
                return Result.Failure<DateTimeOffset>(new DomainError(ErrorCodes.ElementNotFound, $"Element {id} cannot be viewed."));
            }

            // A view always sorts after the previous ones of the user, even if the system clock was set back (or, in the
            // tests, reset): the order of « récemment consultées » is the order of the views, not of the clock.
            var now = clock.GetUtcNow();
            var last = connection.ExecuteScalar<string?>(
                "SELECT MAX(last_viewed_at) FROM user_activity WHERE user_id = @UserId", new { UserId = userId }, transaction);
            if (last is not null && StorageFormat.ParseTimestamp(last) is var previous && previous >= now)
            {
                now = previous.AddMilliseconds(1);
            }

            connection.Execute(
                """
                INSERT INTO user_activity (user_id, element_id, last_viewed_at) VALUES (@UserId, @Id, @At)
                ON CONFLICT (user_id, element_id) DO UPDATE SET last_viewed_at = excluded.last_viewed_at
                """,
                new { UserId = userId, Id = id.ToInt64(), At = StorageFormat.Timestamp(now) },
                transaction);
            return Result.Success(now);
        }, cancellationToken);
    }

    /// <summary>
    /// The latest views of <paramref name="userId"/> (theirs alone) and the latest changes of the project (shared: the
    /// stamp of the last revision of each element). Elements in the trash are left out.
    /// </summary>
    public RecentActivity GetRecent(string userId, int limit)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);
        limit = Math.Clamp(limit, 1, MaxRecent);
        using var connection = Project.Database.OpenConnection();
        var views = connection.Query<(long Id, string At)>(
                """
                SELECT a.element_id, a.last_viewed_at
                FROM user_activity a JOIN elements e ON e.id = a.element_id
                WHERE a.user_id = @UserId AND e.deleted_at IS NULL
                ORDER BY a.last_viewed_at DESC, a.element_id
                LIMIT @Limit
                """, new { UserId = userId, Limit = limit })
            .ToList();
        var edited = connection.Query<long>(
                """
                SELECT id FROM elements
                WHERE deleted_at IS NULL AND kind NOT IN ('root', 'attachment')
                ORDER BY updated_at DESC, id
                LIMIT @Limit
                """, new { Limit = limit })
            .Select(ElementId.FromInt64).ToList();

        var elements = Repository.GetElements([.. views.Select(v => ElementId.FromInt64(v.Id)).Concat(edited)], connection)
            .ToDictionary(e => e.Id);
        return new RecentActivity(
            [.. views.Select(v => new ViewedElement(elements[ElementId.FromInt64(v.Id)], StorageFormat.ParseTimestamp(v.At)))],
            [.. edited.Select(id => elements[id])]);
    }
}
