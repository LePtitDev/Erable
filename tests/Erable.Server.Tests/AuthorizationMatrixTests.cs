using System.Net;
using Erable.Server.Api;
using Erable.Server.Security;
using Erable.Server.Tests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

/// <summary>M4-02: no endpoint without an access rule, and every rule enforced for every kind of caller.</summary>
public sealed class AuthorizationMatrixTests
{
    [Fact]
    public async Task Matrix_EveryEndpointForEveryProfile_MatchesItsDeclaredRule()
    {
        await using var backend = await TestBackend.StartAsync();

        var violations = await AuthorizationMatrix.RunAsync(backend);

        violations.ShouldBeEmpty(string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public async Task Matrix_EndpointWithoutARule_FailsTheRun()
    {
        await using var backend = await TestBackend.StartAsync(beforeStart: app => app.MapGet("/api/v1/forgotten", () => "oops"));

        var violations = await AuthorizationMatrix.RunAsync(backend);

        violations.ShouldContain(v => v.Contains("/api/v1/forgotten", StringComparison.Ordinal) && v.Contains("no access rule", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Server_EndpointWithoutARule_RefusesEveryone()
    {
        await using var backend = await TestBackend.StartAsync(beforeStart: app => app.MapGet("/api/v1/forgotten", () => "oops"));

        using var admin = await backend.Client.GetAsync(new Uri("/api/v1/forgotten", UriKind.Relative), TestContext.Current.CancellationToken);

        admin.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.ProblemCodeAsync()).ShouldBe("access.undeclared");
    }

    [Fact]
    public async Task Matrix_ProjectEndpointOpenToAnyMember_FailsTheDeclarationCheck()
    {
        await using var backend = await TestBackend.StartAsync(beforeStart: app =>
            app.MapDelete("/api/v1/projects/{projectId}/everything", () => "gone").RequireSignIn());

        var violations = await AuthorizationMatrix.RunAsync(backend);

        violations.ShouldContain(v => v.Contains("must declare a project role", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Matrix_ProjectWriteOpenToReaders_MustBeListed()
    {
        await using var backend = await TestBackend.StartAsync(beforeStart: app =>
            app.MapPost("/api/v1/projects/{projectId}/sneaky", () => "ok").RequireProjectRole(Erable.Core.Accounts.ProjectRole.Reader));

        var violations = await AuthorizationMatrix.RunAsync(backend);

        violations.ShouldContain(v => v.Contains("sneaky", StringComparison.Ordinal) && v.Contains("ReaderMayWrite", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Matrix_ExposesTheApiRoutesItChecks()
    {
        await using var backend = await TestBackend.StartAsync();

        var patterns = backend.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText).ToArray();

        patterns.ShouldContain("/api/v1/projects/{projectId}/pages/{id}/content");
        patterns.ShouldContain("/api/v1/auth/login");
        patterns.Length.ShouldBeGreaterThan(60);
    }

    [Fact]
    public async Task Desktop_EveryApiEndpoint_NeedsTheSessionToken()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { Mode = Erable.Server.Hosting.HostMode.Desktop });
        using var anonymous = new HttpClient { BaseAddress = backend.BaseAddress };
        var unprotected = new List<string>();

        foreach (var endpoint in backend.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>())
        {
            var pattern = endpoint.RoutePattern.RawText ?? "?";
            var url = AuthorizationMatrix.Fill(endpoint.RoutePattern, Erable.Core.ElementId.NewRandom());
            var api = ApiRoutes.NonSpaPrefixes.Any(prefix => url.StartsWith(prefix, StringComparison.Ordinal));
            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
            {
                if (!api)
                {
                    // Only the shell of the SPA and the inter-instance call (which has its own token) are open.
                    if (!DesktopOpenRoutes.Contains(pattern))
                    {
                        unprotected.Add($"{method} {pattern}: open without a session token and not listed.");
                    }

                    continue;
                }

                using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));
                using var response = await anonymous.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    unprotected.Add($"{method} {pattern}: answered {(int)response.StatusCode} without a session token.");
                }
            }
        }

        unprotected.ShouldBeEmpty(string.Join(Environment.NewLine, unprotected));
    }

    /// <summary>Routes that need no session token on the desktop: the static shell and the call between instances (own token).</summary>
    private static readonly HashSet<string> DesktopOpenRoutes = new(StringComparer.Ordinal)
    {
        "/index.html",
        "/ipc/activate",
        "{*path:nonfile}",
        "{*path}",
    };
}
