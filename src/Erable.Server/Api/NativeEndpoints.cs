using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Attachments;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Erable.Server.Projects;
using Erable.Server.Security;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <param name="Title">Title of the OS dialog (translated by the SPA).</param>
public sealed record PickFolderRequest(string Title);

/// <param name="Path">The folder chosen, or null when the user cancelled.</param>
public sealed record PickedFolder(string? Path);

/// <param name="Title">Window title: the current project, or « Erable ».</param>
/// <param name="UnsavedChanges">A page has changes that closing the window would lose (F16.R3).</param>
public sealed record WindowState(string Title, bool UnsavedChanges);

/// <summary>
/// The few OS features of the desktop application (docs/architecture.md §10, F16), through <see cref="INativeBridge"/>.
/// In server mode they answer 404 <c>native.unavailable</c>: the SPA hides them there.
/// </summary>
public static class NativeEndpoints
{
    public const string Unavailable = "native.unavailable";
    public const string ExecutableRefused = "attachment.executable";

    public static IEndpointRouteBuilder MapNativeEndpoints(this IEndpointRouteBuilder endpoints, ErableHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var native = endpoints.MapGroup(ApiRoutes.V1 + "/native").WithTags("Native").RequireSignIn();
        native.MapPost("/pick-folder", PickFolder).WithName("PickFolder");
        native.MapPut("/window", UpdateWindow).WithName("UpdateWindow");
        native.MapPost("/window/close", CloseWindow).WithName("CloseWindow");
        native.MapGet("/events", Events).WithName("NativeEvents");

        var project = endpoints.MapGroup(ApiRoutes.V1 + "/projects/{projectId}").WithTags("Attachments");
        project.MapPost("/attachments/{id}/open", OpenAttachment).WithName("OpenAttachmentWithSystem").RequireProjectRole(ProjectRole.Reader);
        project.MapPost("/attachments/{id}/reveal", RevealAttachment).WithName("RevealAttachment").RequireProjectRole(ProjectRole.Reader);

        if (options.Mode == HostMode.Desktop)
        {
            endpoints.MapPost(InstanceRegistry.ActivatePath, Activate).ExcludeFromDescription().PublicAccess();
        }

        return endpoints;
    }

    private static async Task<Results<Ok<PickedFolder>, ProblemHttpResult>> PickFolder(
        PickFolderRequest request, INativeBridge bridge, HttpContext http)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        var path = await bridge.PickFolderAsync(request.Title, http.RequestAborted).ConfigureAwait(false);
        return TypedResults.Ok(new PickedFolder(path));
    }

    private static Results<NoContent, ProblemHttpResult> UpdateWindow(WindowState state, INativeBridge bridge, WindowCoordinator window)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        bridge.SetWindowTitle(string.IsNullOrWhiteSpace(state.Title) ? "Erable" : state.Title.Trim());
        window.SetUnsavedChanges(state.UnsavedChanges);
        return TypedResults.NoContent();
    }

    private static Results<NoContent, ProblemHttpResult> CloseWindow(INativeBridge bridge, WindowCoordinator window)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        window.ApproveClose();
        return TypedResults.NoContent();
    }

    /// <summary>Server-sent events from the shell to the SPA, until it disconnects or the application stops.</summary>
    private static Results<ServerSentEventsResult<NativeEvent>, ProblemHttpResult> Events(
        INativeBridge bridge, WindowCoordinator window, IHostApplicationLifetime lifetime, HttpContext http)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        // Linked, so that a pending stream never holds the shutdown back (F16.R3: no process left behind).
        var stop = CancellationTokenSource.CreateLinkedTokenSource(http.RequestAborted, lifetime.ApplicationStopping);
        http.Response.RegisterForDispose(stop);
        return TypedResults.ServerSentEvents(window.Subscribe(stop.Token));
    }

    /// <summary>Opens a copy of the attachment, named like it, with the application the OS associates with its type.</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> OpenAttachment(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, INativeBridge bridge, HttpContext http)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        var exported = await ExportAsync(projectId, id, workspace, http.RequestAborted).ConfigureAwait(false);
        if (exported.Problem is { } problem)
        {
            return problem;
        }

        if (AttachmentFileName.IsExecutable(exported.Path!))
        {
            return Problems.Problem(StatusCodes.Status422UnprocessableEntity, ExecutableRefused, "Programs and scripts are never run from Erable.");
        }

        await bridge.OpenWithSystemAsync(exported.Path!, http.RequestAborted).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>Shows the copy of the attachment in the OS file manager (the store itself only has content hashes).</summary>
    private static async Task<Results<NoContent, ProblemHttpResult>> RevealAttachment(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, INativeBridge bridge, HttpContext http)
    {
        if (!bridge.IsAvailable)
        {
            return NotAvailable();
        }

        var exported = await ExportAsync(projectId, id, workspace, http.RequestAborted).ConfigureAwait(false);
        if (exported.Problem is { } problem)
        {
            return problem;
        }

        await bridge.RevealInFileManagerAsync(exported.Path!, http.RequestAborted).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    /// <summary>F16.R2: another instance that found this project locked asks this one to come to the front.</summary>
    private static IResult Activate(ActivateRequest request, HttpContext http, InstanceRegistry registry, ProjectWorkspace workspace, INativeBridge bridge)
    {
        if (!registry.IsValidToken(http.Request.Headers[InstanceRegistry.IpcHeader]))
        {
            return TypedResults.Unauthorized();
        }

        if (request.Folder is { } folder && workspace.FindByFolder(folder) is null)
        {
            return TypedResults.NotFound();
        }

        bridge.BringToFront();
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Copies the attachment to <c>&lt;temp&gt;/erable-open/&lt;project&gt;/&lt;id&gt;/&lt;file name&gt;</c>: blobs are stored
    /// under their hash, without a name nor an extension for the OS to recognize.
    /// </summary>
    private static async Task<(string? Path, ProblemHttpResult? Problem)> ExportAsync(
        ElementId projectId, ElementId id, ProjectWorkspace workspace, CancellationToken cancellationToken)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return (null, Problems.ProjectNotOpen(projectId));
        }

        if (service.OpenAttachment(id) is not { } opened)
        {
            return (null, Problems.ElementNotFound(id));
        }

        var (attachment, content) = opened;
        await using (content.ConfigureAwait(false))
        {
            var directory = Path.Combine(Path.GetTempPath(), "erable-open", projectId.ToString(), id.ToString());
            var path = Path.Combine(directory, attachment.FileName);
            if (!File.Exists(path) || new FileInfo(path).Length != attachment.Size)
            {
                Directory.CreateDirectory(directory);
                var temporary = path + ".tmp";
                var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None);
                await using (target.ConfigureAwait(false))
                {
                    await content.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporary, path, overwrite: true);
            }

            return (path, null);
        }
    }

    private static ProblemHttpResult NotAvailable() =>
        Problems.Problem(StatusCodes.Status404NotFound, Unavailable, "Native features are only available in the desktop application.");
}
