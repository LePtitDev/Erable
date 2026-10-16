using System.Collections.Concurrent;
using Erable.Core;

namespace Erable.Server.Realtime;

/// <summary>A live hub connection and the projects it joined.</summary>
public sealed record ConnectedClient(string ConnectionId, string UserId, IReadOnlySet<ElementId> Projects);

/// <summary>
/// Who is connected to which project (M4-03). Presence (M4-04) is built on it, and so is the removal of a member:
/// their connections leave the project group at once, without waiting for the next reconnection.
/// </summary>
public sealed class ProjectConnections
{
    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);

    public void Join(string connectionId, string userId, ElementId projectId)
    {
        var entry = _connections.GetOrAdd(connectionId, _ => new Entry(userId));
        lock (entry)
        {
            entry.Projects.Add(projectId);
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

    /// <summary>Forgets a connection; returns the projects it was in.</summary>
    public IReadOnlySet<ElementId> Remove(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var entry))
        {
            return new HashSet<ElementId>();
        }

        lock (entry)
        {
            return new HashSet<ElementId>(entry.Projects);
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
                if (entry.Projects.Contains(projectId))
                {
                    list.Add(new ConnectedClient(id, entry.UserId, new HashSet<ElementId>(entry.Projects)));
                }
            }
        }

        return list;
    }

    public int Count => _connections.Count;

    private sealed class Entry(string userId)
    {
        public string UserId { get; } = userId;

        public HashSet<ElementId> Projects { get; } = [];
    }
}
