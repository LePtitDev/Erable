using System.Security.Cryptography;
using System.Text;
using Erable.Server.Api;
using Erable.Server.Hosting;

namespace Erable.Server.Security;

/// <summary>How the desktop session token travels (see <see cref="DesktopSecurityMiddleware"/>).</summary>
public static class DesktopSession
{
    /// <summary>Header carrying the token, used once by the SPA to obtain the cookie (and by tests).</summary>
    public const string TokenHeader = "X-Erable-Session";

    /// <summary>HttpOnly, SameSite=Strict cookie carrying the token afterwards.</summary>
    public const string TokenCookie = "erable_session";
}

/// <summary>
/// Protects the desktop loopback server from other local processes and from web pages (docs/architecture.md §9):
/// exact <c>Host</c> check (DNS rebinding), <c>Origin</c> check on unsafe methods (CSRF), and the session token
/// on every API, hub and test request. The static SPA shell stays public: it holds no user data.
/// </summary>
internal sealed class DesktopSecurityMiddleware(RequestDelegate next, ErableHostOptions options)
{
    private readonly byte[] _token = Encoding.UTF8.GetBytes(
        options.SessionToken ?? throw new InvalidOperationException("Desktop mode requires a session token."));

    public Task InvokeAsync(HttpContext context)
    {
        var expectedAuthority = $"{ErableHost.DesktopAddress}:{context.Connection.LocalPort}";

        if (!string.Equals(context.Request.Host.Value, expectedAuthority, StringComparison.Ordinal))
        {
            return Reject(context, StatusCodes.Status400BadRequest, "Invalid host.");
        }

        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method)
            && context.Request.Headers.Origin is [{ } origin]
            && !string.Equals(origin, "http://" + expectedAuthority, StringComparison.Ordinal))
        {
            return Reject(context, StatusCodes.Status403Forbidden, "Cross-origin request refused.");
        }

        if (RequiresToken(context.Request.Path) && !HasValidToken(context.Request))
        {
            return Reject(context, StatusCodes.Status401Unauthorized, "Missing or invalid session token.");
        }

        return next(context);
    }

    private static bool RequiresToken(PathString path) =>
        ApiRoutes.NonSpaPrefixes.Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

    private bool HasValidToken(HttpRequest request) =>
        Matches(request.Headers[DesktopSession.TokenHeader].ToString()) || Matches(request.Cookies[DesktopSession.TokenCookie]);

    /// <summary>Constant-time comparison with the session token.</summary>
    private bool Matches(string? candidate) =>
        !string.IsNullOrEmpty(candidate)
        && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(candidate), _token);

    private static Task Reject(HttpContext context, int status, string title) =>
        TypedResults.Problem(statusCode: status, title: title).ExecuteAsync(context);
}
