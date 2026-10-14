using Erable.Core;
using Erable.Core.Attachments;
using Erable.Server.Hosting;
using Erable.Server.Projects;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace Erable.Server.Api;

/// <summary>Upload and download of attachments (F06, docs/architecture.md §7.3 and §9).</summary>
public static class AttachmentEndpoints
{
    /// <summary>Room for the multipart envelope around the file when checking <c>Content-Length</c> early.</summary>
    private const long MultipartOverhead = 64 * 1024;

    public static RouteGroupBuilder MapAttachmentEndpoints(this RouteGroupBuilder project)
    {
        ArgumentNullException.ThrowIfNull(project);
        project.MapPost("/pages/{id}/attachments", Upload)
            .WithName("UploadAttachment").WithTags("Attachments")
            .Produces(StatusCodes.Status413PayloadTooLarge);
        project.MapGet("/attachments/{id}", Download)
            .WithName("DownloadAttachment").WithTags("Attachments");
        return project;
    }

    /// <summary>
    /// One file per request, streamed from the multipart body to the blob store without buffering (no
    /// <c>IFormFile</c>, which copies the upload to memory or a temporary file first).
    /// </summary>
    private static async Task<Results<Created<AttachmentItem>, ProblemHttpResult>> Upload(
        ElementId projectId, ElementId id, HttpContext http, ProjectWorkspace workspace, AttachmentSettings settings)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        var maxSize = settings.MaxSize;
        if (http.Request.ContentLength > maxSize + MultipartOverhead)
        {
            return TooLarge(maxSize);
        }

        if (!MediaTypeHeaderValue.TryParse(http.Request.ContentType, out var contentType)
            || !contentType.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase)
            || HeaderUtilities.RemoveQuotes(contentType.Boundary).Value is not { Length: > 0 } boundary)
        {
            return Problems.Problem(StatusCodes.Status415UnsupportedMediaType, "attachment.multipartRequired", "Send the file as multipart/form-data.");
        }

        // Our own limit applies to the file; the server-wide request limit (30 MB by default) must not cut it first.
        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySize)
        {
            bodySize.MaxRequestBodySize = maxSize + MultipartOverhead;
        }

        var reader = new MultipartReader(boundary, http.Request.Body);
        while (await reader.ReadNextSectionAsync(http.RequestAborted).ConfigureAwait(false) is { } section)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
                || !disposition.IsFileDisposition())
            {
                continue;
            }

            var fileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                ?? HeaderUtilities.RemoveQuotes(disposition.FileName).Value;
            var added = await service.AddAttachmentAsync(id, fileName, section.Body, maxSize, CurrentUser.Id(http), http.RequestAborted)
                .ConfigureAwait(false);
            if (!added.IsSuccess)
            {
                return added.Error.Code == ErrorCodes.AttachmentTooLarge ? TooLarge(maxSize) : Problems.From(added.Error);
            }

            return TypedResults.Created($"/api/v1/projects/{projectId}/attachments/{added.Value.Id}", AttachmentItem.From(added.Value));
        }

        return Problems.Problem(StatusCodes.Status400BadRequest, "attachment.fileMissing", "The request contains no file.");
    }

    /// <summary>
    /// The content, with <c>Range</c> support (206, F06.R3) and the sha256 as a strong ETag. Only the types the SPA
    /// shows inline are served with their own type and <c>inline</c>; everything else, SVG and HTML included, is an
    /// <c>application/octet-stream</c> download, so that no user file can run script in our origin (§9).
    /// </summary>
    private static Results<FileStreamHttpResult, ProblemHttpResult> Download(ElementId projectId, ElementId id, ProjectWorkspace workspace, HttpContext http)
    {
        if (workspace.Find(projectId) is not { } service)
        {
            return Problems.ProjectNotOpen(projectId);
        }

        if (service.OpenAttachment(id) is not { } opened)
        {
            return Problems.ElementNotFound(id);
        }

        var (attachment, content) = opened;
        var inline = MediaTypes.IsInlineSafe(attachment.Mime);
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment");
        disposition.SetHttpFileName(attachment.FileName);
        var headers = http.Response.Headers;
        headers.ContentDisposition = disposition.ToString();
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        headers.CacheControl = "private, no-cache";
        return TypedResults.Stream(
            content, inline ? attachment.Mime : MediaTypes.OctetStream,
            entityTag: new EntityTagHeaderValue($"\"{attachment.Sha256}\""), enableRangeProcessing: true);
    }

    private static ProblemHttpResult TooLarge(long maxSize) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status413PayloadTooLarge,
            detail: $"The file exceeds the limit of {maxSize} bytes.",
            extensions: new Dictionary<string, object?>(StringComparer.Ordinal) { ["code"] = ErrorCodes.AttachmentTooLarge, ["maxSize"] = maxSize });
}

/// <summary>Attachment limits of this backend (F06.R5); adjustable at runtime by the test endpoints.</summary>
public sealed class AttachmentSettings(ErableHostOptions options)
{
    private long _maxSize = options.MaxAttachmentSize;

    public long MaxSize
    {
        get => Interlocked.Read(ref _maxSize);
        set => Interlocked.Exchange(ref _maxSize, value);
    }
}
