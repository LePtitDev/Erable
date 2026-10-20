using System.Security.Claims;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Projects;
using Erable.Server.Accounts;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace Erable.Server.Security;

/// <summary>Who may call an endpoint (docs/architecture.md §9, docs/testing.md §7).</summary>
public enum Access
{
    /// <summary>Anyone, even anonymous: the SPA shell, the version handshake, the login itself.</summary>
    Public,

    /// <summary>Any signed-in user (the desktop user is always signed in).</summary>
    Authenticated,

    /// <summary>Creates accounts and projects.</summary>
    ServerAdmin,

    /// <summary>Member of the project of the route, at least Reader.</summary>
    ProjectReader,

    /// <summary>Member of the project of the route, at least Editor.</summary>
    ProjectEditor,

    /// <summary>Member of the project of the route, Administrator.</summary>
    ProjectAdmin,
}

/// <summary>
/// The declared access rule of an endpoint, as endpoint metadata. <see cref="AccessControlMiddleware"/> enforces it and
/// the authorization matrix test reads it: an endpoint without one fails closed and fails the suite.
/// </summary>
public sealed record AccessRule(Access Level)
{
    /// <summary>The project role a project-scoped level requires, or null.</summary>
    public ProjectRole? RequiredRole => Level switch
    {
        Access.ProjectReader => ProjectRole.Reader,
        Access.ProjectEditor => ProjectRole.Editor,
        Access.ProjectAdmin => ProjectRole.Admin,
        Access.Public or Access.Authenticated or Access.ServerAdmin => null,
        _ => throw new ArgumentOutOfRangeException(nameof(Level), Level, null),
    };
}

/// <summary>Declaring access rules while mapping endpoints.</summary>
public static class AccessRules
{
    public const string ProjectRouteKey = "projectId";

    public static TBuilder PublicAccess<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.WithMetadata(new AccessRule(Access.Public));

    public static TBuilder RequireSignIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.WithMetadata(new AccessRule(Access.Authenticated));

    public static TBuilder RequireServerAdmin<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder => builder.WithMetadata(new AccessRule(Access.ServerAdmin));

    /// <summary>The route must carry <c>{projectId}</c>.</summary>
    public static TBuilder RequireProjectRole<TBuilder>(this TBuilder builder, ProjectRole role)
        where TBuilder : IEndpointConventionBuilder => builder.WithMetadata(new AccessRule(role switch
        {
            ProjectRole.Reader => Access.ProjectReader,
            ProjectRole.Editor => Access.ProjectEditor,
            ProjectRole.Admin => Access.ProjectAdmin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
        }));

    /// <summary>
    /// Secure by default for the endpoints of a project: those without an explicit rule need Reader to read (GET, HEAD)
    /// and Editor to do anything else. An endpoint that deviates says so with <see cref="RequireProjectRole"/>.
    /// </summary>
    public static TBuilder WithProjectDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpoint =>
        {
            if (endpoint.Metadata.OfType<AccessRule>().Any())
            {
                return;
            }

            var methods = endpoint.Metadata.OfType<HttpMethodMetadata>().SelectMany(m => m.HttpMethods).ToArray();
            var reads = methods.Length > 0 && methods.All(m => HttpMethods.IsGet(m) || HttpMethods.IsHead(m));
            endpoint.Metadata.Add(new AccessRule(reads ? Access.ProjectReader : Access.ProjectEditor));
        });
        return builder;
    }
}

/// <summary>The signed-in user of a request.</summary>
public static class AccessUser
{
    public const string ServerAdminClaim = "erable:server-admin";

    /// <summary>Account login, or <see cref="KnownUsers.Local"/> on the desktop; null when anonymous.</summary>
    public static string? AccountId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static bool IsServerAdmin(this ClaimsPrincipal user) => user.HasClaim(ServerAdminClaim, "1");

    public static ClaimsPrincipal Create(string id, string name, bool serverAdmin)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, id), new(ClaimTypes.Name, name) };
        if (serverAdmin)
        {
            claims.Add(new Claim(ServerAdminClaim, "1"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "erable"));
    }
}

/// <summary>Cookie and header names of a server session.</summary>
public static class SessionCookies
{
    public const string CookieName = "erable_auth";

    /// <summary>The session token a request carries (bearer header first, then cookie), or null.</summary>
    public static string? TokenOf(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return context.Request.Cookies[CookieName] is { Length: > 0 } cookie ? cookie : null;
    }

    public static void Append(HttpContext context, ErableHostOptions options, string token, DateTimeOffset expires) =>
        context.Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Strict: the cookie never accompanies a request started from another site. With the Origin check of
            // AccessControlMiddleware this is the anti-CSRF defence.
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps || options.CookieSecure,
            Path = "/",
            // Relative to the clock of the server (frozen in the tests): an absolute date would be judged by the browser's own clock.
            MaxAge = expires - context.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(),
            IsEssential = true,
        });

    public static void Delete(HttpContext context, ErableHostOptions options) =>
        context.Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps || options.CookieSecure,
            Path = "/",
        });
}

/// <summary>
/// Authenticates the request (desktop: the local user; server: the session cookie or the bearer token) and enforces the
/// <see cref="AccessRule"/> of the endpoint. Runs after routing, so the endpoint metadata is known.
/// </summary>
internal sealed class AccessControlMiddleware(
    RequestDelegate next, ErableHostOptions options, AccountService accounts, ILogger<AccessControlMiddleware> logger)
{
    private const string BearerPrefix = "Bearer ";

    public async Task InvokeAsync(HttpContext context)
    {
        var fromCookie = Authenticate(context);

        if (context.GetEndpoint() is not { } endpoint)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var rule = endpoint.Metadata.GetMetadata<AccessRule>();
        if (rule is null)
        {
            // Fail closed: an endpoint nobody classified is refused, and the authorization matrix test fails.
            ServerLog.UndeclaredAccess(logger, endpoint.DisplayName);
            await Problems.Problem(StatusCodes.Status403Forbidden, "access.undeclared", "This endpoint has no access rule.")
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        if (rule.Level != Access.Public || context.User.Identity?.IsAuthenticated == true)
        {
            if (fromCookie && IsCrossSiteWrite(context))
            {
                await Problems.Problem(StatusCodes.Status403Forbidden, "access.crossOrigin", "Cross-origin request refused.")
                    .ExecuteAsync(context).ConfigureAwait(false);
                return;
            }
        }

        if (Evaluate(context, rule) is { } refusal)
        {
            await refusal.ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>Sets <c>context.User</c>; true when it came from the session cookie.</summary>
    private bool Authenticate(HttpContext context)
    {
        if (options.Mode == HostMode.Desktop)
        {
            // The session token was checked by DesktopSecurityMiddleware: whoever got here is the user of the machine.
            context.User = AccessUser.Create(KnownUsers.Local, KnownUsers.Local, serverAdmin: true);
            return false;
        }

        var fromCookie = false;
        string? token = null;
        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            token = authorization[BearerPrefix.Length..].Trim();
        }
        else if (context.Request.Cookies[SessionCookies.CookieName] is { Length: > 0 } cookie)
        {
            token = cookie;
            fromCookie = true;
        }
        else if (context.Request.Path.StartsWithSegments("/hubs", StringComparison.Ordinal)
            && context.Request.Query["access_token"].ToString() is { Length: > 0 } queryToken)
        {
            // Browsers cannot set a header on a WebSocket: a bearer token travels in the query string, for the hubs only.
            token = queryToken;
        }

        if (token is null || accounts.Authenticate(token) is not { } session)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity());
            return false;
        }

        context.User = AccessUser.Create(session.Account.Id, session.Account.DisplayName, session.Account.IsServerAdmin);
        if (fromCookie && session.Renewed)
        {
            context.Response.OnStarting(() =>
            {
                SessionCookies.Append(context, options, token, session.Session.ExpiresAt);
                return Task.CompletedTask;
            });
        }

        return fromCookie;
    }

    private ProblemHttpResult? Evaluate(HttpContext context, AccessRule rule)
    {
        if (rule.Level == Access.Public)
        {
            return null;
        }

        if (context.User.AccountId() is not { } userId)
        {
            return Problems.Problem(StatusCodes.Status401Unauthorized, "auth.required", "Sign in first.");
        }

        if (rule.RequiredRole is not { } required)
        {
            return rule.Level == Access.ServerAdmin && !context.User.IsServerAdmin()
                ? Problems.Problem(StatusCodes.Status403Forbidden, "access.forbidden", "Reserved to the server administrators.")
                : null;
        }

        if (!ElementId.TryParse(context.Request.RouteValues[AccessRules.ProjectRouteKey]?.ToString(), out var projectId)
            || accounts.RoleOf(projectId, userId) is not { } role)
        {
            // A non-member learns nothing: no difference between "no such project" and "not yours" (F14.R2).
            return Problems.Problem(StatusCodes.Status404NotFound, Problems.ProjectNotFound, "No such project.");
        }

        return ProjectRoles.Allows(role, required)
            ? null
            : Problems.Problem(StatusCodes.Status403Forbidden, "access.forbidden", "Your role does not allow this.");
    }

    /// <summary>
    /// A write sent with the session cookie must come from this site: the Origin header (always set by browsers on
    /// writes) has to name our host, and a request that browsers mark cross-site is refused whatever its headers.
    /// </summary>
    private static bool IsCrossSiteWrite(HttpContext context)
    {
        var method = context.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method))
        {
            return false;
        }

        if (context.Request.Headers.Origin.ToString() is { Length: > 0 } origin)
        {
            return !Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Authority, context.Request.Host.Value, StringComparison.OrdinalIgnoreCase);
        }

        return context.Request.Headers["Sec-Fetch-Site"].ToString() is { Length: > 0 } site && site is not ("same-origin" or "none");
    }
}
