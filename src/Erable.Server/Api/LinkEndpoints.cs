using Erable.Core;
using Erable.Server.Projects;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>Link types and typed links between pages (docs/architecture.md §5.5, §8.1, F09).</summary>
public static class LinkEndpoints
{
    public static RouteGroupBuilder MapLinkEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        var types = project.MapGroup("/link-types").WithTags("Links");
        types.MapGet("/", ListLinkTypes).WithName("ListLinkTypes");
        types.MapPost("/", CreateLinkType).WithName("CreateLinkType");
        types.MapGet("/{id}", GetLinkType).WithName("GetLinkType");
        types.MapPut("/{id}", UpdateLinkType).WithName("UpdateLinkType");

        project.MapGet("/pages/{id}/links", GetPageLinks).WithName("GetPageLinks").WithTags("Links");
        var links = project.MapGroup("/links").WithTags("Links");
        links.MapPost("/", CreateLink).WithName("CreateLink");
        links.MapDelete("/{id}", DeleteLink).WithName("DeleteLink");
        return project;
    }

    private static Results<Ok<LinkTypeItem[]>, ProblemHttpResult> ListLinkTypes(ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Find(projectId) is { } service
            ? TypedResults.Ok(service.ListLinkTypes().Select(t => new LinkTypeItem(t.Id, t.Name, t.Description, t.InverseName, t.LinkCount)).ToArray())
            : Problems.ProjectNotOpen(projectId);

    private static async Task<Results<Created<LinkTypeView>, ProblemHttpResult>> CreateLinkType(
        ElementId projectId, CreateLinkTypeRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var created = await service.CreateLinkTypeAsync(
            request.ParentId, request.Name, request.Index, request.Description, request.InverseName, CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/link-types/{created.Value.Id}", ViewOf(service, users, created.Value.Id)!)
            : Problems.From(created.Error);
    }

    private static Results<Ok<LinkTypeView>, ProblemHttpResult> GetLinkType(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return ViewOf(service, users, id) is { } view ? TypedResults.Ok(view) : Problems.ElementNotFound(id);
    }

    private static async Task<Results<Ok<LinkTypeView>, ProblemHttpResult>> UpdateLinkType(
        ElementId projectId, ElementId id, UpdateLinkTypeRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var updated = await service.UpdateLinkTypeAsync(id, request.Description, request.InverseName, CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(ViewOf(service, users, id)!) : Problems.From(updated.Error);
    }

    private static Results<Ok<PageLinksView>, ProblemHttpResult> GetPageLinks(ElementId projectId, ElementId id, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return service.GetPageLinks(id) is { } links
            ? TypedResults.Ok(new PageLinksView([.. links.Outgoing.Select(Item)], [.. links.Incoming.Select(Item)]))
            : Problems.ElementNotFound(id);
    }

    private static async Task<Results<Created<LinkItem>, ProblemHttpResult>> CreateLink(
        ElementId projectId, CreateLinkRequest request, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var created = await service.CreateLinkAsync(request.SourceId, request.LinkTypeId, request.TargetId, CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/links/{created.Value.Id}",
                new LinkItem(created.Value.Id, created.Value.SourceId, created.Value.LinkTypeId, created.Value.TargetId))
            : Problems.From(created.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteLink(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var deleted = await service.DeleteLinkAsync(id, http.RequestAborted).ConfigureAwait(false);
        return deleted.IsSuccess ? TypedResults.NoContent() : Problems.From(deleted.Error);
    }

    private static LinkTypeView? ViewOf(ProjectService service, UserDirectory users, ElementId id)
    {
        if (service.GetLinkType(id) is not { } type || service.GetElement(id) is not { } found)
        {
            return null;
        }

        return new LinkTypeView(ProjectEndpoints.Details(found, service, users), type.Description, type.InverseName, type.LinkCount);
    }

    private static PageLinkItem Item(PageLink link) => new(link.LinkId, link.LinkTypeId, link.TypeName, link.Label, link.OtherId, link.OtherName);
}
