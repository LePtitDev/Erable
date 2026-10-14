using System.Security.Cryptography;
using System.Text;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Erable.Server.Api;

public static class SessionEndpoints
{
    /// <summary>
    /// Desktop mode: exchanges the session token (received by the SPA in the URL fragment, never sent to the
    /// server in a URL) for an HttpOnly cookie, so that every later request — including WebSocket handshakes
    /// and <c>&lt;img&gt;</c> loads — is authenticated without exposing the token to scripts or logs.
    /// </summary>
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/session", Results<NoContent, ProblemHttpResult> (
                HttpContext context,
                ErableHostOptions options,
                [FromHeader(Name = DesktopSession.TokenHeader)] string token) =>
            {
                if (options.Mode != HostMode.Desktop || options.SessionToken is null)
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
                }

                // Already checked by DesktopSecurityMiddleware; repeated here so the endpoint is safe on its own.
                if (!CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(options.SessionToken)))
                {
                    return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized);
                }

                context.Response.Cookies.Append(DesktopSession.TokenCookie, options.SessionToken, new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Path = "/",
                    IsEssential = true,
                });
                return TypedResults.NoContent();
            })
            .WithName("CreateDesktopSession");
        return endpoints;
    }
}
