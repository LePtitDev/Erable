using Erable.Core;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>
/// Business errors as RFC 9457 <c>ProblemDetails</c>, with the stable <see cref="DomainError.Code"/> in a <c>code</c>
/// extension that the SPA translates (<c>errors.&lt;code&gt;</c>).
/// </summary>
public static class Problems
{
    public const string ProjectNotFound = "project.notFound";
    public const string PreconditionRequired = "request.ifMatchRequired";

    public static ProblemHttpResult From(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Problem(StatusOf(error.Code), error.Code, error.Message, error.Extensions);
    }

    /// <summary>A problem with the stable <c>code</c> and optional machine-readable extensions (a category loop, blocking pages…).</summary>
    public static ProblemHttpResult Problem(int status, string code, string? detail = null, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        var all = new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code };
        foreach (var (key, value) in extensions ?? new Dictionary<string, object?>())
        {
            all[key] = value;
        }

        return TypedResults.Problem(statusCode: status, detail: detail, extensions: all);
    }

    public static ProblemHttpResult ProjectNotOpen(ElementId projectId) =>
        Problem(StatusCodes.Status404NotFound, ProjectNotFound, $"Project {projectId} is not open.");

    public static ProblemHttpResult FolderProjectsDisabled() =>
        Problem(StatusCodes.Status404NotFound, "feature.folderProjects", "This server hosts its projects itself; paths are not used.");

    public static ProblemHttpResult ElementNotFound(ElementId id) =>
        Problem(StatusCodes.Status404NotFound, ErrorCodes.ElementNotFound, $"Element {id} does not exist.");

    private static int StatusOf(string code) => code switch
    {
        ErrorCodes.ElementNotFound or ErrorCodes.RevisionNotFound or ErrorCodes.LinkNotFound or ErrorCodes.LinkTypeNotFound or ErrorCodes.TrashEntryNotFound => StatusCodes.Status404NotFound,
        StorageErrors.Locked or StorageErrors.AlreadyExists or StorageErrors.MigrationRequired or "project.duplicateId" => StatusCodes.Status409Conflict,
        StorageErrors.MigrationFailed => StatusCodes.Status500InternalServerError,
        "page.revisionConflict" => StatusCodes.Status409Conflict,
        "auth.invalidCredentials" or "auth.required" => StatusCodes.Status401Unauthorized,
        "auth.tooManyAttempts" => StatusCodes.Status429TooManyRequests,
        "server.invalidUrl" => StatusCodes.Status400BadRequest,
        "server.notFound" => StatusCodes.Status404NotFound,
        "server.unreachable" or "server.notErable" => StatusCodes.Status502BadGateway,
        ErrorCodes.PropertyKeyTaken or ErrorCodes.LinkDuplicate => StatusCodes.Status409Conflict,
        StorageErrors.Inaccessible => StatusCodes.Status403Forbidden,
        // Everything else is a request that a business rule refuses.
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
