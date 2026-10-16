using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Projects;
using Erable.Core.Tree;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Erable.Server.Projects;
using Erable.Server.Users;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

/// <summary>Projects, tree, elements, pages, references (docs/architecture.md §8.1).</summary>
public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder api)
    {
        ArgumentNullException.ThrowIfNull(api);

        var projects = api.MapGroup("/projects").WithTags("Projects");
        projects.MapGet("/", ListOpen).WithName("ListOpenProjects");
        projects.MapPost("/", Create).WithName("CreateProject");
        projects.MapPost("/open", Open).WithName("OpenProject");
        projects.MapPost("/inspect", Inspect).WithName("InspectFolder");
        projects.MapGet("/{projectId}", Get).WithName("GetProject");

        var project = projects.MapGroup("/{projectId}");
        project.MapGet("/tree", GetTree).WithName("GetTree").WithTags("Tree");
        project.MapGet("/elements/{id}", GetElement).WithName("GetElement").WithTags("Elements");
        project.MapPatch("/elements/{id}", UpdateElement).WithName("UpdateElement").WithTags("Elements");
        project.MapGet("/elements/{id}/backlinks", GetBacklinks).WithName("GetBacklinks").WithTags("Elements");
        project.MapGet("/resolve", Resolve).WithName("ResolveElements").WithTags("Elements");
        project.MapPost("/folders", CreateFolder).WithName("CreateFolder").WithTags("Elements");
        project.MapPost("/pages", CreatePage).WithName("CreatePage").WithTags("Pages");
        project.MapGet("/pages/{id}", GetPage).WithName("GetPage").WithTags("Pages");
        project.MapGet("/pages/{id}/content", GetPageContent).WithName("GetPageContent").WithTags("Pages");
        project.MapPut("/pages/{id}/content", SavePageContent).WithName("SavePageContent").WithTags("Pages");
        project.MapAttachmentEndpoints();
        project.MapStructureEndpoints();
        project.MapLinkEndpoints();
        project.MapTrashEndpoints();
        project.MapSearchEndpoints();
        project.MapRecentEndpoints();
        project.MapHistoryEndpoints();

        var me = api.MapGroup("/me").WithTags("Me");
        me.MapGet("/", GetMe).WithName("GetMe");
        me.MapGet("/recent-projects", ListRecent).WithName("ListRecentProjects");
        me.MapDelete("/recent-projects/{projectId}", RemoveRecent).WithName("RemoveRecentProject");
        me.MapPut("/recent-projects/{projectId}/last-element", RecordLastElement).WithName("RecordLastElement");
        me.MapGet("/preferences", GetPreferences).WithName("GetPreferences");
        me.MapPut("/preferences", PutPreferences).WithName("PutPreferences");
        me.MapGet("/ui-state/{key}", GetUiState).WithName("GetUiState");
        me.MapPut("/ui-state/{key}", PutUiState).WithName("PutUiState");
        me.MapDelete("/ui-state/{key}", DeleteUiState).WithName("DeleteUiState");
        return api;
    }

    // ─── Projects ───────────────────────────────────────────────────────────

    private static Ok<ProjectSummary[]> ListOpen(ProjectWorkspace workspace) =>
        TypedResults.Ok(workspace.OpenProjects.Select(p => Summary(p.Project)).ToArray());

    private static Results<Created<ProjectSummary>, ProblemHttpResult> Create(
        CreateProjectRequest request, ProjectWorkspace workspace, UserProfile profile)
    {
        var created = workspace.Create(request.Folder, request.Name, request.ContentLanguage, profile);
        if (!created.IsSuccess)
        {
            return Problems.From(created.Error);
        }

        var summary = Summary(created.Value.Project);
        return TypedResults.Created($"/api/v1/projects/{summary.Id}", summary);
    }

    private static async Task<Results<Ok<ProjectSummary>, ProblemHttpResult>> Open(
        OpenProjectRequest request, ProjectWorkspace workspace, UserProfile profile, ErableHostOptions options, INativeBridge bridge,
        HttpContext http)
    {
        var opened = workspace.Open(request.Folder, profile, request.ConfirmMigration);
        if (opened.IsSuccess)
        {
            return TypedResults.Ok(Summary(opened.Value.Project));
        }

        // F16.R2: the project is open in another window of the application, which comes to the front.
        if (opened.Error.Code == StorageErrors.Locked && options.Mode == HostMode.Desktop)
        {
            await InstanceRegistry.ActivateOwnerAsync(ErableHost.ProfileDirectory(options), request.Folder, bridge.AllowForeground, http.RequestAborted)
                .ConfigureAwait(false);
        }

        return Problems.From(opened.Error);
    }

    private static Ok<FolderInspection> Inspect(InspectFolderRequest request)
    {
        var (status, manifest) = ProjectStore.Inspect(request.Folder);
        return TypedResults.Ok(new FolderInspection(status, manifest?.Name, manifest?.ProjectId, manifest?.FormatVersion));
    }

    private static Results<Ok<ProjectSummary>, ProblemHttpResult> Get(ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Find(projectId) is { } service ? TypedResults.Ok(Summary(service.Project)) : Problems.ProjectNotOpen(projectId);

    // ─── Tree and elements ──────────────────────────────────────────────────

    private static Results<Ok<ProjectTree>, ProblemHttpResult> GetTree(ElementId projectId, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var tree = service.LoadTree();
        var language = service.Project.Manifest.ContentLanguage;
        var duplicates = tree.DuplicateNames();
        var nodes = tree.Nodes
            .Where(n => n.Kind != ElementKind.Attachment)
            .OrderBy(n => n.SortOrder).ThenBy(n => n.Id.Value)
            .Select(n => new TreeNodeItem(
                n.Id, n.Kind, n.ParentId.IsNone ? null : n.ParentId, DisplayName(n, language), n.SortOrder,
                duplicates.Contains(n.Id)))
            .ToArray();
        int Count(RootDefinition root) => tree.Descendants(root.Id).Count(n => n.Kind == root.ItemKind);
        var counts = new RootCounts(Count(Roots.Pages), Count(Roots.Categories), Count(Roots.Properties), Count(Roots.Links));
        return TypedResults.Ok(new ProjectTree(nodes, counts));
    }

    private static Results<Ok<ElementDetails>, ProblemHttpResult> GetElement(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        return service.GetElement(id) is { } found
            ? TypedResults.Ok(Details(found, service, users))
            : Problems.ElementNotFound(id);
    }

    private static async Task<Results<Ok<ElementDetails>, ProblemHttpResult>> UpdateElement(
        ElementId projectId, ElementId id, UpdateElementRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var updated = await service.UpdateElementAsync(id, request.Name, request.ParentId, request.Index, CurrentUser.Id(http), http.RequestAborted)
            .ConfigureAwait(false);
        return updated.IsSuccess ? TypedResults.Ok(Details(service.GetElement(id)!, service, users)) : Problems.From(updated.Error);
    }

    private static Results<Ok<BacklinkItem[]>, ProblemHttpResult> GetBacklinks(ElementId projectId, ElementId id, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (service.GetElement(id) is null)
        {
            return Problems.ElementNotFound(id);
        }

        var tree = service.LoadTree();
        var language = service.Project.Manifest.ContentLanguage;
        return TypedResults.Ok(service.GetBacklinks(id).Select(b => new BacklinkItem(
            b.Source.Id, b.Source.Kind, b.Source.Name,
            [.. tree.Ancestors(b.Source.Id).Reverse().Select(n => PathItemOf(n, language))],
            b.References.Count, b.References.Any(r => r.IsEmbed))).ToArray());
    }

    /// <summary><c>?ids=A,B,C</c>: current name, kind, path and state of each ID (F04.R1–R2).</summary>
    private static Results<Ok<ResolveResult>, ProblemHttpResult> Resolve(ElementId projectId, [FromQuery] string ids, ProjectWorkspace workspace)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var parsed = new List<ElementId>();
        foreach (var part in ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!ElementId.TryParse(part, out var id))
            {
                return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidId", $"'{part}' is not an element ID.");
            }

            parsed.Add(id);
        }

        if (parsed.Count > ProjectService.MaxResolve)
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "request.tooManyIds", $"At most {ProjectService.MaxResolve} IDs per call.");
        }

        var language = service.Project.Manifest.ContentLanguage;
        var resolved = service.Resolve(parsed);
        var attachments = service.Project.Repository
            .GetAttachments([.. resolved.Where(r => r.Element?.Kind == ElementKind.Attachment).Select(r => r.Id)])
            .ToDictionary(a => a.Id);
        return TypedResults.Ok(new ResolveResult([.. resolved.Select(r => new ResolvedItem(
            r.Id, r.State, r.Element?.Kind, r.Element?.Name, [.. r.Path.Select(n => PathItemOf(n, language))],
            attachments.TryGetValue(r.Id, out var attachment) ? AttachmentItem.From(attachment) : null))]));
    }

    private static async Task<Results<Created<ElementDetails>, ProblemHttpResult>> CreateFolder(
        ElementId projectId, CreateFolderRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var created = await service.CreateElementAsync(
            ElementKind.Folder, request.ParentId, request.Name, request.Index, CurrentUser.Id(http), cancellationToken: http.RequestAborted)
            .ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/elements/{created.Value.Id}", Details(service.GetElement(created.Value.Id)!, service, users))
            : Problems.From(created.Error);
    }

    // ─── Pages ──────────────────────────────────────────────────────────────

    private static async Task<Results<Created<ElementDetails>, ProblemHttpResult>> CreatePage(
        ElementId projectId, CreatePageRequest request, ProjectWorkspace workspace, UserDirectory users, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var created = await service.CreateElementAsync(
            ElementKind.Page, request.ParentId, request.Name, request.Index, CurrentUser.Id(http), request.Markdown, http.RequestAborted)
            .ConfigureAwait(false);
        return created.IsSuccess
            ? TypedResults.Created($"/api/v1/projects/{projectId}/pages/{created.Value.Id}", Details(service.GetElement(created.Value.Id)!, service, users))
            : Problems.From(created.Error);
    }

    private static Results<Ok<PageView>, ProblemHttpResult> GetPage(ElementId projectId, ElementId id, ProjectWorkspace workspace, UserDirectory users)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (service.GetElement(id) is not { Element.Kind: ElementKind.Page } found)
        {
            return Problems.ElementNotFound(id);
        }

        var repository = service.Project.Repository;
        var content = repository.GetPageContent(id);
        var tree = service.LoadTree();
        var subPages = tree.Contains(id)
            ? tree.Children(id).Where(n => n.Kind == ElementKind.Page).Select(n => new ChildPage(n.Id, n.Name)).ToArray()
            : [];
        var attachments = repository.GetAttachments(id).Select(AttachmentItem.From).ToArray();
        var structure = StructureEndpoints.PagePropertiesOf(service, id);
        return TypedResults.Ok(new PageView(
            Details(found, service, users), content?.Markdown ?? string.Empty, subPages, attachments,
            structure?.Categories ?? [], structure?.Properties ?? []));
    }

    private static Results<Ok<PageContent>, ProblemHttpResult> GetPageContent(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (service.Project.Repository.GetPageContent(id) is not { } content
            || service.Project.Repository.GetElement(id) is not { DeletedAt: null })
        {
            return Problems.ElementNotFound(id);
        }

        http.Response.Headers.ETag = $"\"{content.Revision}\"";
        return TypedResults.Ok(new PageContent(content.Markdown, content.Revision));
    }

    /// <summary>Optimistic concurrency: <c>If-Match: "&lt;revision&gt;"</c> is mandatory; a stale revision answers 409.</summary>
    private static async Task<Results<Ok<PageSavedResult>, ProblemHttpResult>> SavePageContent(
        ElementId projectId, ElementId id, SavePageContentRequest request, ProjectWorkspace workspace, HttpContext http,
        [FromHeader(Name = "If-Match")] string? ifMatch)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (!TryParseRevision(ifMatch, out var expected))
        {
            return Problems.Problem(StatusCodes.Status428PreconditionRequired, Problems.PreconditionRequired,
                "Send If-Match with the revision the edit started from.");
        }

        var structure = request.Categories is null && request.Properties is null
            ? null
            : new PageStructureInput(
                request.Categories,
                request.Properties?.Select(p => (p.PropertyId, System.Text.Json.JsonSerializer.SerializeToElement(p.Value))).ToArray());
        var saved = await service.SavePageContentAsync(id, request.Markdown, expected, CurrentUser.Id(http), structure, http.RequestAborted)
            .ConfigureAwait(false);
        if (!saved.IsSuccess)
        {
            return Problems.From(saved.Error);
        }

        http.Response.Headers.ETag = $"\"{saved.Value.Revision}\"";
        return TypedResults.Ok(new PageSavedResult(saved.Value.Revision, saved.Value.UpdatedAt, saved.Value.UpdatedBy));
    }

    // ─── Me ─────────────────────────────────────────────────────────────────

    /// <summary>The current user: « moi » in dates and authors, « Bonjour, … » on the home screen.</summary>
    private static Ok<CurrentUserInfo> GetMe(UserDirectory users, ErableHostOptions options, UserProfiles profiles, HttpContext http)
    {
        var id = CurrentUser.Id(http);
        // An e2e test creates its projects in its own folder: tests in parallel never collide.
        var folder = profiles.TestSlotOf(http) is { } slot
            ? Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "e2e", slot, "projects")
            : DefaultProjectsFolder(options);
        return TypedResults.Ok(new CurrentUserInfo(id, users.NameOf(id), folder));
    }

    /// <summary>Desktop: <c>Documents/Erable</c>; server: <c>&lt;data&gt;/projects</c> (docs/architecture.md §7.1).</summary>
    private static string DefaultProjectsFolder(ErableHostOptions options)
    {
        if (options.Mode == HostMode.Desktop)
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documents.Length > 0 ? documents : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Erable");
        }

        return Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "projects");
    }

    // ─── Recent projects ────────────────────────────────────────────────────

    private static Ok<RecentProjectItem[]> ListRecent(UserProfile profile) =>
        TypedResults.Ok(profile.GetRecentProjects()
            .Select(r => new RecentProjectItem(r.ProjectId, r.Name, r.Folder, r.LastOpenedAt, r.LastElementId, ProjectStore.Inspect(r.Folder).Status))
            .ToArray());

    private static Results<NoContent, ProblemHttpResult> RemoveRecent(ElementId projectId, UserProfile profile) =>
        profile.RemoveRecentProject(projectId)
            ? TypedResults.NoContent()
            : Problems.Problem(StatusCodes.Status404NotFound, Problems.ProjectNotFound, "Not in the recent projects.");

    private static Results<NoContent, ProblemHttpResult> RecordLastElement(
        ElementId projectId, RecordLastElementRequest request, UserProfile profile) =>
        profile.RecordLastElement(projectId, request.ElementId)
            ? TypedResults.NoContent()
            : Problems.Problem(StatusCodes.Status404NotFound, Problems.ProjectNotFound, "Not in the recent projects.");

    // ─── UI state ───────────────────────────────────────────────────────────

    /// <summary>Language, theme and shortcuts of the user (F15), written into index.html before the first paint.</summary>
    private static Ok<UserPreferences> GetPreferences(UserProfile profile) => TypedResults.Ok(profile.GetPreferences());

    private static Results<Ok<UserPreferences>, ProblemHttpResult> PutPreferences(UserPreferences preferences, UserProfile profile)
    {
        if (preferences.Validate() is { } code)
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, code, "Unsupported preference value.");
        }

        profile.SavePreferences(preferences);
        return TypedResults.Ok(profile.GetPreferences());
    }

    /// <summary>
    /// Small JSON documents of the interface (panel sizes, expanded tree nodes, drafts…), kept in the user profile
    /// because the browser stores nothing durable (decision Q7). 404 when never written.
    /// </summary>
    private static Results<Ok<JsonNode>, ProblemHttpResult> GetUiState(string key, UserProfile profile)
    {
        if (!UiStateKey.IsValid(key))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidKey", "Keys: 1 to 100 letters, digits, '.', '-' or '_'.");
        }

        return profile.Read<JsonNode>(UiStateKey.FileOf(key)) is { } value
            ? TypedResults.Ok(value)
            : Problems.Problem(StatusCodes.Status404NotFound, "uiState.notFound");
    }

    private static Results<NoContent, ProblemHttpResult> PutUiState(string key, JsonNode value, UserProfile profile, HttpContext http)
    {
        if (!UiStateKey.IsValid(key))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidKey", "Keys: 1 to 100 letters, digits, '.', '-' or '_'.");
        }

        if (http.Request.ContentLength > UiStateKey.MaxBytes || value.ToJsonString().Length > UiStateKey.MaxBytes)
        {
            return Problems.Problem(StatusCodes.Status413PayloadTooLarge, "uiState.tooLarge", $"At most {UiStateKey.MaxBytes} bytes.");
        }

        profile.Write(UiStateKey.FileOf(key), value);
        return TypedResults.NoContent();
    }

    private static Results<NoContent, ProblemHttpResult> DeleteUiState(string key, UserProfile profile)
    {
        if (!UiStateKey.IsValid(key))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "request.invalidKey", "Keys: 1 to 100 letters, digits, '.', '-' or '_'.");
        }

        profile.Delete(UiStateKey.FileOf(key));
        return TypedResults.NoContent();
    }

    // ─── Mapping ────────────────────────────────────────────────────────────

    private static ProjectSummary Summary(OpenedProject project) => new(
        project.Manifest.ProjectId, project.Manifest.Name, project.Folder, project.Manifest.ContentLanguage,
        project.Manifest.FormatVersion, project.Manifest.CreatedAt);

    internal static ElementDetails Details(ElementWithPath found, ProjectService service, UserDirectory users)
    {
        var e = found.Element;
        var language = service.Project.Manifest.ContentLanguage;
        var name = e.Kind == ElementKind.Root && Roots.Find(e.Id) is { } root ? Roots.Label(root, language) : e.Name;
        return new ElementDetails(
            e.Id, e.Kind, e.ParentId.IsNone ? null : e.ParentId, name, e.Revision, e.CreatedAt, e.CreatedBy, e.UpdatedAt, e.UpdatedBy,
            e.DeletedAt is not null, [.. found.Path.Select(n => PathItemOf(n, language))], users.NameOf(e.CreatedBy), users.NameOf(e.UpdatedBy));
    }

    internal static PathItem PathItemOf(TreeNode node, string language) => new(node.Id, node.Kind, DisplayName(node, language));

    /// <summary>Roots are labelled in the project's content language (docs/architecture.md §11.1).</summary>
    private static string DisplayName(TreeNode node, string language) =>
        node.IsRoot && Roots.Find(node.Id) is { } root ? Roots.Label(root, language) : node.Name;

    private static bool TryParseRevision(string? ifMatch, out long revision)
    {
        revision = 0;
        var value = ifMatch?.Trim();
        if (value is null)
        {
            return false;
        }

        if (value.StartsWith("W/", StringComparison.Ordinal))
        {
            value = value[2..];
        }

        return long.TryParse(value.Trim('"'), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out revision);
    }
}

/// <summary>Who makes the request. Desktop and M1 server mode have one local user; accounts arrive with M4-01.</summary>
public static class CurrentUser
{
    public static string Id(HttpContext context)
    {
        _ = context;
        return KnownUsers.Local;
    }
}
