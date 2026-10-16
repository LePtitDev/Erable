using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Erable.Core;

namespace Erable.Server.Realtime;

/// <summary>What a user is doing in a project (F14.R3): reading or editing.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PresenceMode>))]
public enum PresenceMode
{
    Read,
    Edit,
}

/// <summary>Where a connection is in a project: the element shown, how, and the section being edited (the heading around the cursor).</summary>
public sealed record PresenceState(ElementId? ElementId, PresenceMode Mode, string? Section, long Sequence);

/// <summary>A user present in a project, as the other clients show them (avatars, « Camille R. modifie aussi cette page »).</summary>
public sealed record PresenceUser(string UserId, string Name, ElementId? ElementId, PresenceMode Mode, string? Section);

/// <summary>A live hub connection and the projects it joined.</summary>
public sealed record ConnectedClient(string ConnectionId, string UserId, IReadOnlySet<ElementId> Projects);

/// <summary>
/// Who is connected to which project, and what they are doing there (M4-03, M4-04). The removal of a member uses it
/// too: their connections leave the project group at once, without waiting for the next reconnection. A connection
/// that goes away, by itself or because the server stopped hearing from it, takes its presence along.
/// </summary>
public sealed class ProjectConnections
{
    public const int MaxSectionLength = 200;

    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);
    private long _sequence;

    public void Join(string connectionId, string userId, ElementId projectId)
    {
        var entry = _connections.GetOrAdd(connectionId, _ => new Entry(userId));
        lock (entry)
        {
            entry.Projects[projectId] = new PresenceState(null, PresenceMode.Read, null, Interlocked.Increment(ref _sequence));
        }
    }

    public void Leave(string connectionId, ElementId projectId)
    {
        if (_connections.TryGetValue(connectionId, out var entry))
        {
            lock (entry)
            {
                entry.Projects.Remove(projectId);
            }
        }
    }

    /// <summary>Records what a connection does in a project it joined; false when it did not join it.</summary>
    public bool SetState(string connectionId, ElementId projectId, ElementId? elementId, PresenceMode mode, string? section)
    {
        if (!_connections.TryGetValue(connectionId, out var entry))
        {
            return false;
        }

        var clean = section?.Trim() is { Length: > 0 } trimmed ? Truncate(trimmed) : null;
        lock (entry)
        {
            if (!entry.Projects.ContainsKey(projectId))
            {
                return false;
            }

            entry.Projects[projectId] = new PresenceState(
                elementId is { IsNone: false } ? elementId : null,
                mode,
                mode == PresenceMode.Edit ? clean : null,
                Interlocked.Increment(ref _sequence));
            return true;
        }
    }

    /// <summary>Forgets a connection; returns the projects it was in.</summary>
    public IReadOnlySet<ElementId> Remove(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var entry))
        {
            return new HashSet<ElementId>();
        }

        lock (entry)
        {
            return new HashSet<ElementId>(entry.Projects.Keys);
        }
    }

    /// <summary>The connections of a project.</summary>
    public IReadOnlyList<ConnectedClient> Of(ElementId projectId)
    {
        var list = new List<ConnectedClient>();
        foreach (var (id, entry) in _connections)
        {
            lock (entry)
            {
                if (entry.Projects.ContainsKey(projectId))
                {
                    list.Add(new ConnectedClient(id, entry.UserId, new HashSet<ElementId>(entry.Projects.Keys)));
                }
            }
        }

        return list;
    }

    /// <summary>
    /// The users present in a project: one line per user, with what their most recent connection does (a user with two
    /// tabs is shown where they last acted). Ordered by name so that the avatars do not jump about.
    /// </summary>
    public IReadOnlyList<PresenceUser> Snapshot(ElementId projectId, Func<string, string> nameOf)
    {
        ArgumentNullException.ThrowIfNull(nameOf);
        var latest = new Dictionary<string, PresenceState>(StringComparer.Ordinal);
        foreach (var entry in _connections.Values)
        {
            lock (entry)
            {
                if (entry.Projects.TryGetValue(projectId, out var state)
                    && (!latest.TryGetValue(entry.UserId, out var known) || state.Sequence > known.Sequence))
                {
                    latest[entry.UserId] = state;
                }
            }
        }

        return
        [
            .. latest
                .Select(l => new PresenceUser(l.Key, nameOf(l.Key), l.Value.ElementId, l.Value.Mode, l.Value.Section))
                .OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(u => u.UserId, StringComparer.Ordinal),
        ];
    }

    /// <summary>The projects an account is connected to, on any of its connections.</summary>
    public IReadOnlySet<ElementId> ProjectsOf(string userId)
    {
        var projects = new HashSet<ElementId>();
        foreach (var entry in _connections.Values.Where(e => e.UserId == userId))
        {
            lock (entry)
            {
                projects.UnionWith(entry.Projects.Keys);
            }
        }

        return projects;
    }

    public int Count => _connections.Count;

    private static string Truncate(string value) => value.Length <= MaxSectionLength ? value : value[..MaxSectionLength];

    private sealed class Entry(string userId)
    {
        public string UserId { get; } = userId;

        public Dictionary<ElementId, PresenceState> Projects { get; } = [];
    }
}
