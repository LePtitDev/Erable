using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Elements;
using Erable.Server.Projects;
using Erable.Server.Security;
using Erable.Server.Users;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

// Recently viewed and edited (F11).

/// <summary>An element the user viewed, newest first.</summary>
public sealed record ViewedItem(ElementId Id, ElementKind Kind, string Name, DateTimeOffset ViewedAt);

/// <summary>An element changed lately by anybody, with the author of its last revision (F11.R2).</summary>
public sealed record EditedItem(ElementId Id, ElementKind Kind, string Name, DateTimeOffset EditedAt, string EditedBy, string EditedByName);

/// <summary>The two lists of the « récents » panels: <c>viewed</c> is the user's own, <c>edited</c> is shared.</summary>
public sealed record RecentActivityView(IReadOnlyList<ViewedItem> Viewed, IReadOnlyList<EditedItem> Edited);

/// <summary>Counts a view of an element (the SPA sends it after two seconds of display, F11.R1).</summary>
public sealed record RecordViewRequest(ElementId ElementId);

/// <summary>Recently viewed and edited elements of a project (docs/architecture.md §8.1 <c>me/recent</c>).</summary>
public static class RecentEndpoints
{
    public static RouteGroupBuilder MapRecentEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var me = project.MapGroup("/me").WithTags("Recent");
        me.MapGet("/recent", GetRecent).WithName("GetRecent");
        me.MapPost("/views", RecordView).WithName("RecordView").RequireProjectRole(ProjectRole.Reader);
        return project;
    }

    private static Results<Ok<RecentActivityView>, ProblemHttpResult> GetRecent(
        ElementId projectId, ProjectWorkspace workspace, UserDirectory users, HttpContext http, [FromQuery] int? limit = null)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var recent = service.GetRecent(CurrentUser.Id(http), limit ?? 10);
        return TypedResults.Ok(new RecentActivityView(
            [.. recent.Viewed.Select(v => new ViewedItem(v.Element.Id, v.Element.Kind, v.Element.Name, v.ViewedAt))],
            [.. recent.Edited.Select(e => new EditedItem(e.Id, e.Kind, e.Name, e.UpdatedAt, e.UpdatedBy, users.NameOf(e.UpdatedBy)))]));
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> RecordView(
        ElementId projectId, RecordViewRequest request, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var recorded = await service.RecordViewAsync(CurrentUser.Id(http), request.ElementId, http.RequestAborted).ConfigureAwait(false);
        return recorded.IsSuccess ? TypedResults.NoContent() : Problems.From(recorded.Error);
    }
}
