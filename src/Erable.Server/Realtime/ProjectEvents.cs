using Erable.Core;
using Erable.Server.Users;
using Microsoft.AspNetCore.SignalR;

namespace Erable.Server.Realtime;

/// <summary>Sends the events of a project to the clients that joined it.</summary>
public sealed class ProjectEvents(IHubContext<ProjectHub, IProjectClient> hub, ProjectConnections connections, UserDirectory users)
{
    public IReadOnlyList<PresenceUser> PresenceOf(ElementId projectId) => connections.Snapshot(projectId, users.NameOf);

    public Task PresenceChangedAsync(ElementId projectId) =>
        Group(projectId).PresenceChanged(new PresenceChangedEvent(projectId, PresenceOf(projectId)));

    private IProjectClient Group(ElementId projectId) => hub.Clients.Group(ProjectHub.GroupOf(projectId));

    public Task ElementChangedAsync(ElementId projectId, ElementId elementId, long? revision, string by) =>
        Group(projectId).ElementChanged(new ElementChangedEvent(projectId, elementId, revision, by));

    public Task ElementMovedAsync(ElementId projectId, ElementId elementId, string by) =>
        Group(projectId).ElementMoved(new ElementMovedEvent(projectId, elementId, by));

    public Task ElementDeletedAsync(ElementId projectId, ElementId elementId, string by) =>
        Group(projectId).ElementDeleted(new ElementDeletedEvent(projectId, elementId, by));

    public Task TreeChangedAsync(ElementId projectId, string by) =>
        Group(projectId).TreeChanged(new TreeChangedEvent(projectId, by));

    public Task MembersChangedAsync(ElementId projectId) => Group(projectId).MembersChanged(new MembersChangedEvent(projectId));

    /// <summary>An account was disabled: it leaves every project it was connected to.</summary>
    public async Task RevokeEverywhereAsync(string userId)
    {
        foreach (var projectId in connections.ProjectsOf(userId))
        {
            await RevokeAsync(projectId, userId).ConfigureAwait(false);
        }
    }

    /// <summary>A member lost their access (removed, or the account was disabled): their connections leave the project now.</summary>
    public async Task RevokeAsync(ElementId projectId, string userId)
    {
        foreach (var connection in connections.Of(projectId).Where(c => c.UserId == userId))
        {
            connections.Leave(connection.ConnectionId, projectId);
            await hub.Groups.RemoveFromGroupAsync(connection.ConnectionId, ProjectHub.GroupOf(projectId)).ConfigureAwait(false);
        }

        await PresenceChangedAsync(projectId).ConfigureAwait(false);
    }
}
