using Erable.Core;
using Erable.Core.Projects;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;

namespace Erable.Server.Realtime;

/// <summary>
/// Announces the writes of a project to the other clients (M4-03, F14.R4): once a write request has been answered with
/// a success — its transaction is committed by then — the events of the project go out. A failed write announces
/// nothing, and neither do the requests that only record the caller's own data (views, previews).
/// </summary>
internal sealed class ProjectEventsMiddleware(RequestDelegate next, ProjectEvents events, ILogger<ProjectEventsMiddleware> logger)
{
    /// <summary>Writes that change nothing the other clients show.</summary>
    private static readonly HashSet<string> Silent = new(StringComparer.Ordinal)
    {
        "RecordView",
        "PreviewPageProperties",
        "OpenAttachmentWithSystem",
        "RevealAttachment",
        // Told by the endpoints themselves (MembersChanged): the tree and the elements did not change.
        "SetMember",
        "RemoveMember",
    };

    public async Task InvokeAsync(HttpContext context)
    {
        await next(context).ConfigureAwait(false);

        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
            || context.Response.StatusCode is < 200 or > 299
            || !ElementId.TryParse(context.Request.RouteValues[AccessRules.ProjectRouteKey]?.ToString(), out var projectId)
            || context.GetEndpoint() is not { } endpoint
            || endpoint.Metadata.GetMetadata<AccessRule>() is not { RequiredRole: not null })
        {
            return;
        }

        var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? string.Empty;
        if (Silent.Contains(name))
        {
            return;
        }

        var by = context.User.AccountId() ?? KnownUsers.Local;
        var id = ElementId.TryParse(context.Request.RouteValues["id"]?.ToString(), out var parsed) ? parsed : ElementId.None;
        try
        {
            switch (name)
            {
                case "SavePageContent":
                    // A page save changes that page only: the tree and the names around are untouched.
                    await events.ElementChangedAsync(projectId, id, RevisionOf(context), by).ConfigureAwait(false);
                    break;
                case "UpdateElement":
                    await events.ElementMovedAsync(projectId, id, by).ConfigureAwait(false);
                    await events.TreeChangedAsync(projectId, by).ConfigureAwait(false);
                    break;
                case "DeleteElement":
                    await events.ElementDeletedAsync(projectId, id, by).ConfigureAwait(false);
                    await events.TreeChangedAsync(projectId, by).ConfigureAwait(false);
                    break;
                default:
                    if (!id.IsNone)
                    {
                        await events.ElementChangedAsync(projectId, id, null, by).ConfigureAwait(false);
                    }

                    await events.TreeChangedAsync(projectId, by).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The write is done: a client that missed the event reloads at its next reconnection.
            ServerLog.EventNotSent(logger, ex, projectId, name);
        }
    }

    private static long? RevisionOf(HttpContext context) =>
        long.TryParse(context.Response.Headers[HeaderNames.ETag].ToString().Trim('"'), out var revision) ? revision : null;
}
