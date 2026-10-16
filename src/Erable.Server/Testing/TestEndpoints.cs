#if ERABLE_TEST_ENDPOINTS
using System.Collections.Concurrent;
using Erable.Core;
using Erable.Server.Accounts;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Projects;
using Erable.Server.Security;
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
        var group = endpoints.MapGroup(Prefix).ExcludeFromDescription().PublicAccess();
        group.MapGet("/ping", () => TypedResults.NoContent());
        group.MapPost("/reset", Reset);
        group.MapPost("/clock", SetClock);
        group.MapGet("/blobs", ListBlobs);
        group.MapPost("/limits", SetLimits);
        group.MapGet("/doctor", Doctor);
        group.MapPost("/close", Close);
        group.MapPost("/login-as/{user}", LoginAs);
        group.MapPost("/expire-sessions", ExpireSessions);
    }

    /// <summary>
    /// Gives the calling test slot (one per e2e test file) a fresh copy of a reference project and a fresh profile,
    /// selected by the <c>erable-test-slot</c> cookie. Each copy gets its own <c>projectId</c>: copies of the same
    /// fixture are open side by side, so test files run in parallel. <c>fixture=none</c> only resets the profile; <c>fixture=large</c> copies the project named by <c>ERABLE_LARGE_PROJECT</c>.
    /// </summary>
    private static Results<Ok<TestResetResult>, ProblemHttpResult> Reset(
        [FromQuery] string slot, [FromQuery] string? fixture, [FromQuery] string? user, [FromQuery] bool? other, HttpContext http, ErableHostOptions options,
        ProjectWorkspace workspace, UserProfiles profiles, TestSlots slots, ProjectCatalog catalog, AccountService accounts)
    {
        if (!UserProfiles.IsValidSlot(slot))
        {
            return Problems.Problem(StatusCodes.Status400BadRequest, "test.invalidSlot", "slot: 1 to 64 letters, digits, '-' or '_'.");
        }

        fixture ??= "chroniques";
        var source = fixture switch
        {
            "none" => null,
            // The large project of the performance tests (M3-06) is generated, not versioned: its folder is given by the
            // environment (`erable.benchmarks generate --light`).
            "large" => Environment.GetEnvironmentVariable("ERABLE_LARGE_PROJECT") ?? string.Empty,
            _ => Path.Combine(options.FixturesDirectory ?? string.Empty, fixture),
        };
        if (source is not null && (fixture.Contains('.', StringComparison.Ordinal) || !ManifestFile.Exists(source)))
        {
            return Problems.Problem(StatusCodes.Status404NotFound, "test.unknownFixture", $"No reference project '{fixture}' in the fixtures folder.");
        }

        lock (slots.GateOf(slot))
        {
            foreach (var previous in slots.Take(slot))
            {
                workspace.Close(previous);
                if (accounts.Enabled)
                {
                    accounts.Store.UnregisterProject(previous);
                }
            }

            // The slot root: holds the profile of the test and, below it, one per user.
            var root = profiles.ForTestSlot(slot);
            if (Directory.Exists(root.Directory))
            {
                Directory.Delete(root.Directory, recursive: true);
            }

            http.Response.Cookies.Append(UserProfiles.TestSlotCookie, slot, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });

            // The test is signed in as `user` (default: the administrator of the reference project). An empty `user`
            // leaves it anonymous, to see the login screen.
            var signedIn = accounts.Enabled ? (user ?? "admin") : null;
            if (signedIn is { Length: > 0 })
            {
                if (accounts.Store.GetAccount(signedIn) is null)
                {
                    return Problems.Problem(StatusCodes.Status404NotFound, "test.unknownUser", $"No test user '{signedIn}'.");
                }

                var issued = accounts.StartSession(signedIn, ServerStore.SessionKindCookie);
                SessionCookies.Append(http, options, issued.Token, issued.ExpiresAt);
            }

            if (source is null)
            {
                return TypedResults.Ok(new TestResetResult(null, null, null, null));
            }

            // A new folder per reset: the previous copy may still be held for a moment by SQLite or an antivirus.
            var folder = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "e2e", slot, Guid.NewGuid().ToString("N")[..8]);
            CopyDirectory(source, folder);
            var manifest = ManifestFile.Read(folder).Value;
            ManifestFile.Write(folder, manifest with { ProjectId = ElementId.NewRandom() });
            // Whoever opens the copy leaves it among their recent projects: a user who is no member of it (E2E-14-04) must not.
            var opener = signedIn is { Length: > 0 } && TestUsers.Read(options).Any(u => u.Id == signedIn && u.Role is not null) ? signedIn : null;
            var profile = profiles.ForTestSlot(slot, opener);
            var opened = workspace.Open(folder, profile);
            if (!opened.IsSuccess)
            {
                return Problems.From(opened.Error);
            }

            var project = opened.Value.Project;
            var projectId = project.Manifest.ProjectId;
            slots.Add(slot, projectId);
            ElementId? otherProjectId = null;
            if (accounts.Enabled)
            {
                // Members of the reference project: the roles of users.json (docs/features.md §2.3).
                catalog.Register(opened.Value, "admin");
                foreach (var testUser in TestUsers.Read(options).Where(u => u.Role is not null))
                {
                    accounts.Store.SetMember(projectId, testUser.Id, testUser.Role!.Value);
                }

                // « externe » belongs to another project only (E2E-14-04).
                if (other == true)
                {
                    var otherFolder = Path.Combine(options.DataDirectory ?? Path.GetTempPath(), "e2e", slot, "other-" + Guid.NewGuid().ToString("N")[..8]);
                    var created = workspace.Create(otherFolder, "Autre projet", "fr", profile: null, author: "externe");
                    if (!created.IsSuccess)
                    {
                        return Problems.From(created.Error);
                    }

                    catalog.Register(created.Value, "externe");
                    otherProjectId = created.Value.Project.Manifest.ProjectId;
                    slots.Add(slot, otherProjectId.Value);
                }
            }

            return TypedResults.Ok(new TestResetResult(projectId, project.Manifest.Name, project.Folder, otherProjectId));
        }
    }

    /// <summary>
    /// Opens a session without the login form (docs/features.md §1.4). <c>?token=true</c> returns a bearer token
    /// instead of setting the cookie; <c>?slot=</c> joins the profile slot of a test (a second browser context).
    /// </summary>
    private static Results<NoContent, Ok<TestToken>, ProblemHttpResult> LoginAs(
        string user, [FromQuery] bool? token, [FromQuery] string? slot, HttpContext http, ErableHostOptions options, AccountService accounts)
    {
        if (!accounts.Enabled || accounts.Store.GetAccount(user) is null)
        {
            return Problems.Problem(StatusCodes.Status404NotFound, "test.unknownUser", $"No test user '{user}'.");
        }

        if (slot is not null)
        {
            if (!UserProfiles.IsValidSlot(slot))
            {
                return Problems.Problem(StatusCodes.Status400BadRequest, "test.invalidSlot", "slot: 1 to 64 letters, digits, '-' or '_'.");
            }

            http.Response.Cookies.Append(UserProfiles.TestSlotCookie, slot, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });
        }

        if (token == true)
        {
            return TypedResults.Ok(new TestToken(accounts.StartSession(user, ServerStore.SessionKindBearer).Token));
        }

        var issued = accounts.StartSession(user, ServerStore.SessionKindCookie);
        SessionCookies.Append(http, options, issued.Token, issued.ExpiresAt);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Ends the session of the caller (E2E-14-09: the session expires while a page is being edited). Only that one: the
    /// other tests run against the same backend at the same time.
    /// </summary>
    private static NoContent ExpireSessions(HttpContext http, AccountService accounts)
    {
        if (SessionCookies.TokenOf(http) is { } token)
        {
            accounts.EndSession(token);
        }

        return TypedResults.NoContent();
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

    /// <summary><c>erable doctor</c> on a project open in this backend (run after each e2e test, docs/testing.md §6).</summary>
    private static Results<Ok<DoctorReport>, ProblemHttpResult> Doctor([FromQuery] ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Find(projectId) is { } service
            ? TypedResults.Ok(ProjectDoctor.Diagnose(service.Project, fix: false))
            : Problems.ProjectNotOpen(projectId);

    /// <summary>Closes a project (releases its lock), so that a test can delete or alter its folder (E2E-01-03, E2E-01-04).</summary>
    private static Results<NoContent, ProblemHttpResult> Close([FromQuery] ElementId projectId, ProjectWorkspace workspace) =>
        workspace.Close(projectId) ? TypedResults.NoContent() : Problems.ProjectNotOpen(projectId);

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

internal sealed record TestResetResult(ElementId? ProjectId, string? Name, string? Folder, ElementId? OtherProjectId);

internal sealed record TestToken(string Token);

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

    public IReadOnlySet<ElementId> Of(string slot) =>
        _projects.TryGetValue(slot, out var ids) ? ids.ToHashSet() : [];

    public IReadOnlyList<ElementId> Take(string slot) => _projects.TryRemove(slot, out var ids) ? ids : [];
}
#endif
