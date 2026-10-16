using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>
/// The authorization matrix (docs/testing.md §7): every endpoint the server exposes, called by every kind of user, and
/// the answer compared with the access rule the endpoint declares. An endpoint without a rule fails the run, which is
/// the protection against the most common security bug: the endpoint nobody thought about.
/// </summary>
public static class AuthorizationMatrix
{
    /// <summary>What the access layer did with a request.</summary>
    public enum Outcome
    {
        /// <summary>Let through (the handler may still answer 400, 404, 415… for its own reasons).</summary>
        Allowed,
        Unauthorized,
        Forbidden,
        NotFound,
        Undeclared,
    }

    /// <summary>A kind of caller. <c>Role</c> is the role in the project of the run; null for a non-member.</summary>
    public sealed record Profile(string Name, string? Login, bool ServerAdmin, ProjectRole? Role);

    public static readonly IReadOnlyList<Profile> Profiles =
    [
        new("anonymous", null, false, null),
        new("outsider", "externe", false, null),
        new("reader", "lea", false, ProjectRole.Reader),
        new("editor", "camille", false, ProjectRole.Editor),
        new("projectAdmin", "malo", false, ProjectRole.Admin),
        new("serverAdmin", "root", true, null),
    ];

    /// <summary>Endpoints a project-scoped route declares for writing but that a reader legitimately calls (own data, previews).</summary>
    public static readonly IReadOnlySet<string> ReaderMayWrite = new HashSet<string>(StringComparer.Ordinal)
    {
        "RecordView",
        "PreviewPageProperties",
        "OpenAttachmentWithSystem",
        "RevealAttachment",
    };

    /// <summary>The access rule an endpoint must have, given who calls it.</summary>
    public static Outcome Expected(AccessRule rule, Profile profile)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(profile);
        return rule.Level switch
        {
            Access.Public => Outcome.Allowed,
            Access.Authenticated => profile.Login is null ? Outcome.Unauthorized : Outcome.Allowed,
            Access.ServerAdmin => profile.Login is null ? Outcome.Unauthorized : profile.ServerAdmin ? Outcome.Allowed : Outcome.Forbidden,
            Access.ProjectReader or Access.ProjectEditor or Access.ProjectAdmin =>
                profile.Login is null ? Outcome.Unauthorized
                : profile.Role is not { } role ? Outcome.NotFound
                : ProjectRoles.Allows(role, rule.RequiredRole!.Value) ? Outcome.Allowed : Outcome.Forbidden,
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Level, null),
        };
    }

    /// <summary>Prepares a project and one account per profile, then runs the whole matrix; returns every violation found.</summary>
    public static async Task<IReadOnlyList<string>> RunAsync(TestBackend backend)
    {
        ArgumentNullException.ThrowIfNull(backend);
        var violations = new List<string>();
        var project = await backend.CreateProjectAsync("Matrice");
        var other = await backend.CreateProjectAsync("Autre");
        var store = backend.Accounts.Store;
        foreach (var profile in Profiles.Where(p => p.Login is not null))
        {
            _ = backend.ClientFor(profile.Login!, profile.ServerAdmin);
            if (profile.Role is { } role)
            {
                store.SetMember(project.Id, profile.Login!, role);
            }
        }

        store.SetMember(other.Id, "externe", ProjectRole.Admin);

        var endpoints = backend.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        foreach (var endpoint in endpoints)
        {
            var pattern = endpoint.RoutePattern.RawText ?? "?";
            var rule = endpoint.Metadata.GetMetadata<AccessRule>();
            if (rule is null)
            {
                violations.Add($"{pattern}: no access rule is declared (use PublicAccess, RequireSignIn, RequireServerAdmin or RequireProjectRole).");
                continue;
            }

            var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
            foreach (var method in methods)
            {
                CheckDeclaration(endpoint, method, rule, violations);
                var url = Fill(endpoint.RoutePattern, project.Id);
                foreach (var profile in Profiles)
                {
                    var actual = await CallAsync(backend, profile, method, url, rule);
                    var expected = Expected(rule, profile);
                    if (actual != expected)
                    {
                        violations.Add($"{method} {pattern} as {profile.Name}: expected {expected}, got {actual} (rule {rule.Level}).");
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// A second opinion on the declarations themselves: whatever an endpoint of a project says, a write needs an editor
    /// and a read needs a reader, unless it is one of the few listed exceptions.
    /// </summary>
    private static void CheckDeclaration(RouteEndpoint endpoint, string method, AccessRule rule, List<string> violations)
    {
        var pattern = endpoint.RoutePattern.RawText ?? "?";
        // /me/... holds the caller's own data about a project (recent entries): a project the user lost must stay removable from it.
        if (!pattern.Contains("{projectId}", StringComparison.Ordinal) || rule.Level == Access.Public
            || pattern.StartsWith("/api/v1/me/", StringComparison.Ordinal))
        {
            return;
        }

        var name = endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? pattern;
        var reads = HttpMethods.IsGet(method) || HttpMethods.IsHead(method);
        if (rule.Level is Access.Authenticated or Access.ServerAdmin)
        {
            violations.Add($"{method} {pattern}: a project endpoint must declare a project role, not {rule.Level}.");
        }
        else if (!reads && rule.Level == Access.ProjectReader && !ReaderMayWrite.Contains(name))
        {
            violations.Add($"{method} {pattern} ({name}): a write open to readers must be listed in ReaderMayWrite.");
        }
        else if (reads && rule.Level == Access.ProjectEditor)
        {
            violations.Add($"{method} {pattern} ({name}): a read that needs an editor is probably a mistake.");
        }
    }

    private static async Task<Outcome> CallAsync(TestBackend backend, Profile profile, string method, string url, AccessRule rule)
    {
        // A fresh session per call: some endpoints (logout) end the session they are called with.
        using var client = profile.Login is null ? backend.NewAnonymousClient() : backend.ClientFor(profile.Login, profile.ServerAdmin);
        using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(url, UriKind.Relative));
        if (!HttpMethods.IsGet(method) && !HttpMethods.IsHead(method))
        {
            request.Content = JsonContent.Create(new JsonObject());
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, TestContext.Current.CancellationToken);
        return await ClassifyAsync(response, rule);
    }

    private static async Task<Outcome> ClassifyAsync(HttpResponseMessage response, AccessRule rule)
    {
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return Outcome.Allowed;
        }

        JsonNode? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<JsonNode>(TestContext.Current.CancellationToken);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or HttpRequestException or IOException)
        {
            return Outcome.Allowed;
        }

        var code = body?["code"]?.GetValue<string>();
        var projectScoped = rule.RequiredRole is not null;
        return (response.StatusCode, code) switch
        {
            (HttpStatusCode.Unauthorized, "auth.required") => Outcome.Unauthorized,
            (HttpStatusCode.Forbidden, "access.forbidden") => Outcome.Forbidden,
            (HttpStatusCode.Forbidden, "access.undeclared") => Outcome.Undeclared,
            (HttpStatusCode.NotFound, "project.notFound") when projectScoped => Outcome.NotFound,
            _ => Outcome.Allowed,
        };
    }

    /// <summary>The URL of an endpoint with sample values in its parameters; <paramref name="projectId"/> is a real project.</summary>
    public static string Fill(RoutePattern pattern, ElementId projectId)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var path = new System.Text.StringBuilder();
        foreach (var segment in pattern.PathSegments)
        {
            path.Append('/');
            foreach (var part in segment.Parts)
            {
                switch (part)
                {
                    case RoutePatternLiteralPart literal:
                        path.Append(literal.Content);
                        break;
                    case RoutePatternSeparatorPart separator:
                        path.Append(separator.Content);
                        break;
                    case RoutePatternParameterPart parameter:
                        path.Append(parameter.Name switch
                        {
                            "projectId" => projectId.ToString(),
                            "id" or "propertyId" or "elementId" => ElementId.NewRandom().ToString(),
                            "revision" => "1",
                            "documentName" => "v1",
                            _ => "x",
                        });
                        break;
                }
            }
        }

        return path.Length == 0 ? "/" : path.ToString();
    }
}
