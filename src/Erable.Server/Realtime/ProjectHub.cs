using Erable.Core;
using Erable.Server.Accounts;
using Erable.Server.Projects;
using Erable.Server.Security;
using Microsoft.AspNetCore.SignalR;

namespace Erable.Server.Realtime;

/// <summary>The element of a project changed (content, properties, links…); <c>Revision</c> is set for a page save.</summary>
public sealed record ElementChangedEvent(ElementId ProjectId, ElementId ElementId, long? Revision, string By);

/// <summary>An element was renamed, moved or reordered.</summary>
public sealed record ElementMovedEvent(ElementId ProjectId, ElementId ElementId, string By);

/// <summary>An element went to the trash.</summary>
public sealed record ElementDeletedEvent(ElementId ProjectId, ElementId ElementId, string By);

/// <summary>The shape or the names of the tree changed: the clients reload what shows names and paths.</summary>
public sealed record TreeChangedEvent(ElementId ProjectId, string By);

/// <summary>What the server tells the clients of a project (docs/architecture.md §8.2).</summary>
public interface IProjectClient
{
    Task ElementChanged(ElementChangedEvent change);

    Task ElementMoved(ElementMovedEvent move);

    Task ElementDeleted(ElementDeletedEvent deletion);

    Task TreeChanged(TreeChangedEvent change);
}

/// <summary>Answer of <see cref="ProjectHub.JoinProject"/>.</summary>
public sealed record JoinResult(bool Joined);

/// <summary>
/// <c>/hubs/project</c> (M4-03): a client joins the group of the project it shows, if it is a member, and receives the
/// events of that project only (F14.R4). The events are sent by the server after each committed write
/// (<see cref="ProjectEventsMiddleware"/>); clients never send any.
/// </summary>
public sealed class ProjectHub(AccountService accounts, ProjectWorkspace workspace, ProjectConnections connections) : Hub<IProjectClient>
{
    public static string GroupOf(ElementId projectId) => "project:" + projectId;

    /// <summary>Joins the project; false (nothing else) for a project the user is not a member of or that is not open.</summary>
    public async Task<JoinResult> JoinProject(string projectId)
    {
        if (!ElementId.TryParse(projectId, out var id) || workspace.Find(id) is null
            || Context.User?.AccountId() is not { } userId || accounts.RoleOf(id, userId) is null)
        {
            return new JoinResult(false);
        }

        connections.Join(Context.ConnectionId, userId, id);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupOf(id)).ConfigureAwait(false);
        return new JoinResult(true);
    }

    public async Task LeaveProject(string projectId)
    {
        if (ElementId.TryParse(projectId, out var id))
        {
            connections.Leave(Context.ConnectionId, id);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupOf(id)).ConfigureAwait(false);
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        // The groups forget the connection by themselves.
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
