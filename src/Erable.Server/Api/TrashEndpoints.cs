using Erable.Core;
using Erable.Server.Projects;
using Erable.Server.Users;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>Deleting elements and the trash (docs/architecture.md §8.1, §7.4, F12).</summary>
public static class TrashEndpoints
{
    public static RouteGroupBuilder MapTrashEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.MapGet("/elements/{id}/impact", GetImpact).WithName("GetDeleteImpact").WithTags("Elements");
        project.MapDelete("/elements/{id}", DeleteElement).WithName("DeleteElement").WithTags("Elements");

        var trash = project.MapGroup("/trash").WithTags("Trash");
        trash.MapGet("/", ListTrash).WithName("ListTrash");
        trash.MapPost("/{id}/restore", Restore).WithName("RestoreFromTrash");
        trash.MapDelete("/{id}", PurgeEntry).WithName("PurgeTrashEntry");
        trash.MapDelete("/", EmptyTrash).WithName("EmptyTrash");
        return project;
    }

    private static Results<Ok<DeleteImpactView>, ProblemHttpResult> GetImpact(ElementId projectId, ElementId id, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return service.GetDeleteImpact(id) is { } impact
            ? TypedResults.Ok(new DeleteImpactView(
                impact.Element.Id, impact.Element.Kind, impact.Element.Name,
                [.. impact.ReferencedBy.Select(i => new ImpactItemView(i.Id, i.Name))],
                impact.SubPages, impact.Descendants, impact.Links, impact.Pages, impact.Values))
            : Problems.ElementNotFound(id);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteElement(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var deleted = await service.DeleteElementAsync(id, CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        return deleted.IsSuccess ? TypedResults.NoContent() : Problems.From(deleted.Error);
    }

    private static Results<Ok<TrashEntryView[]>, ProblemHttpResult> ListTrash(ElementId projectId, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var language = service.Project.Manifest.ContentLanguage;
        return TypedResults.Ok(service.ListTrash().Select(e => new TrashEntryView(
            e.Id, e.Kind, e.Name, e.DeletedAt, users.NameOf(e.DeletedBy), e.Descendants,
            [.. e.Path.Select(n => ProjectEndpoints.PathItemOf(n, language))], e.ParentAvailable)).ToArray());
    }

    private static async Task<Results<Ok<RestoreView>, ProblemHttpResult>> Restore(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var restored = await service.RestoreAsync(id, CurrentUser.Id(http), http.RequestAborted).ConfigureAwait(false);
        if (!restored.IsSuccess)
        {
            return Problems.From(restored.Error);
        }

        var result = restored.Value;
        // Roots have a name of their own language: the display name, like in paths.
        var parent = service.LoadTree().Find(result.ParentId);
        var parentName = parent is null ? string.Empty : ProjectEndpoints.PathItemOf(parent, service.Project.Manifest.ContentLanguage).Name;
        return TypedResults.Ok(new RestoreView(
            result.Element.Id, result.Element.Kind, result.Element.Name, result.ParentId, parentName, result.Relocated, result.Restored));
    }

    private static async Task<Results<Ok<PurgeView>, ProblemHttpResult>> PurgeEntry(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var purged = await service.PurgeAsync([id], http.RequestAborted).ConfigureAwait(false);
        return purged.IsSuccess ? TypedResults.Ok(new PurgeView(purged.Value)) : Problems.From(purged.Error);
    }

    private static async Task<Results<Ok<PurgeView>, ProblemHttpResult>> EmptyTrash(ElementId projectId, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var purged = await service.PurgeAsync(null, http.RequestAborted).ConfigureAwait(false);
        return purged.IsSuccess ? TypedResults.Ok(new PurgeView(purged.Value)) : Problems.From(purged.Error);
    }
}
