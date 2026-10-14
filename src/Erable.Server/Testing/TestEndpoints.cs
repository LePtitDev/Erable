#if ERABLE_TEST_ENDPOINTS
using System.Collections.Concurrent;
using Erable.Core;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Projects;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Testing;

/// <summary>Test-only endpoints (docs/features.md §1.4). Compiled out of Release builds.</summary>
internal static class TestEndpoints
{
    public const string Prefix = "/_test";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup(Prefix).ExcludeFromDescription();
        group.MapGet("/ping", () => TypedResults.NoContent());
        group.MapPost("/reset", Reset);
        group.MapPost("/clock", SetClock);
        group.MapGet("/blobs", ListBlobs);
        group.MapPost("/limits", SetLimits);
    }

    /// <summary>
    /// Gives the calling test slot (one per e2e test file) a fresh copy of a reference project and a fresh profile,
    /// selected by the <c>erable-test-slot</c> cookie. Each copy gets its own <c>projectId</c>: copies of the same
    /// fixture are open side by side, so test files run in parallel. <c>fixture=none</c> only resets the profile.
    /// </summary>
    private static Results<Ok<TestResetResult>, ProblemHttpResult> Reset(
        [FromQuery] string slot, [FromQuery] string? fixture, HttpContext http, ErableHostOptions options,
        ProjectWorkspace workspace, UserProfiles profiles, TestSlots slots)
    {
        if (!UserProfiles.IsValidSlot(slot))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "test.invalidSlot", "slot: 1 to 64 letters, digits, '-' or '_'.");
        }

        fixture ??= "chroniques";
        var source = fixture == "none" ? null : Path.Combine(options.FixturesDirectory ?? string.Empty, fixture);
        if (source is not null && (fixture.Contains('.', StringComparison.Ordinal) || !ManifestFile.Exists(source)))
        {
            return Problems.Problem(StatusCodes.Status404NotFound, "test.unknownFixture", $"No reference project '{fixture}' in the fixtures folder.");
        }

        lock (slots.GateOf(slot))
        {
            foreach (var previous in slots.Take(slot))
            {
                workspace.Close(previous);
            }

            var profile = profiles.ForTestSlot(slot);
            if (Directory.Exists(profile.Directory))
            {
                Directory.Delete(profile.Directory, recursive: true);
            }

            http.Response.Cookies.Append(UserProfiles.TestSlotCookie, slot, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });

            if (source is null)
            {
                return TypedResults.Ok(new TestResetResult(null, null, null));
            }

            // A new folder per reset: the previous copy may still be held for a moment by SQLite or an antivirus.
            var folder = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "e2e", slot, Guid.NewGuid().ToString("N")[..8]);
            CopyDirectory(source, folder);
            var manifest = ManifestFile.Read(folder).Value;
            ManifestFile.Write(folder, manifest with { ProjectId = ElementId.NewRandom() });
            var opened = workspace.Open(folder, profile);
            if (!opened.IsSuccess)
            {
                return Problems.From(opened.Error);
            }

            var project = opened.Value.Project;
            slots.Add(slot, project.Manifest.ProjectId);
            return TypedResults.Ok(new TestResetResult(project.Manifest.ProjectId, project.Manifest.Name, project.Folder));
        }
    }

    /// <summary>Moves the backend clock (ticking from <c>now</c>); no body or <c>now: null</c> restores the reference date.</summary>
    private static Results<NoContent, ProblemHttpResult> SetClock(TestClockRequest? request, TimeProvider clock)
    {
        if (clock is not TestClock testClock)
        {
            return Problems.Problem(StatusCodes.Status409Conflict, "test.clockFixed", "This backend runs with a clock supplied by the host.");
        }

        testClock.Set(request?.Now ?? TestClock.ReferenceDate);
        return TypedResults.NoContent();
    }

    /// <summary>Stored blobs of a project (deduplication checks, E2E-06-08).</summary>
    private static Results<Ok<TestBlobs>, ProblemHttpResult> ListBlobs([FromQuery] ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Find(projectId) is { } service
            ? TypedResults.Ok(new TestBlobs([.. service.Project.Blobs.List().Order(StringComparer.Ordinal)]))
            : Problems.ProjectNotOpen(projectId);

    /// <summary>Changes the attachment size limit (E2E-06-07); <c>maxAttachmentSize: null</c> restores the configured one.</summary>
    private static NoContent SetLimits(TestLimitsRequest request, AttachmentSettings settings, ErableHostOptions options)
    {
        settings.MaxSize = request.MaxAttachmentSize ?? options.MaxAttachmentSize;
        return TypedResults.NoContent();
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative == ProjectFiles.Lock)
            {
                continue;
            }

            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }
}

internal sealed record TestResetResult(ElementId? ProjectId, string? Name, string? Folder);

internal sealed record TestClockRequest(DateTimeOffset? Now);

internal sealed record TestBlobs(IReadOnlyList<string> Hashes);

internal sealed record TestLimitsRequest(long? MaxAttachmentSize);

/// <summary>Projects opened by <c>/_test/reset</c> for each slot, closed at the next reset of that slot.</summary>
internal sealed class TestSlots
{
    private readonly ConcurrentDictionary<string, List<ElementId>> _projects = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, object> _gates = new(StringComparer.Ordinal);

    public object GateOf(string slot) => _gates.GetOrAdd(slot, _ => new object());

    public void Add(string slot, ElementId projectId) => _projects.GetOrAdd(slot, _ => []).Add(projectId);

    public IReadOnlyList<ElementId> Take(string slot) => _projects.TryRemove(slot, out var ids) ? ids : [];
}
#endif
