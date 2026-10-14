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
        return Problem(StatusOf(error.Code), error.Code, error.Message);
    }

    public static ProblemHttpResult Problem(int status, string code, string? detail = null) =>
        TypedResults.Problem(
            statusCode: status,
            detail: detail,
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = code });

    public static ProblemHttpResult ProjectNotOpen(ElementId projectId) =>
        Problem(StatusCodes.Status404NotFound, ProjectNotFound, $"Project {projectId} is not open.");

    public static ProblemHttpResult ElementNotFound(ElementId id) =>
        Problem(StatusCodes.Status404NotFound, ErrorCodes.ElementNotFound, $"Element {id} does not exist.");

    private static int StatusOf(string code) => code switch
    {
        ErrorCodes.ElementNotFound => StatusCodes.Status404NotFound,
        StorageErrors.Locked or StorageErrors.AlreadyExists or "project.duplicateId" => StatusCodes.Status409Conflict,
        "page.revisionConflict" => StatusCodes.Status409Conflict,
        StorageErrors.Inaccessible => StatusCodes.Status403Forbidden,
        // Everything else is a request that a business rule refuses.
        _ => StatusCodes.Status422UnprocessableEntity,
    };
}
