using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>Credentials of a login. <c>IssueToken</c> asks for a bearer token instead of a cookie (desktop connected, M4-07).</summary>
public sealed record LoginRequest(string Login, string Password, bool IssueToken = false);

/// <summary>The session just opened. <c>Token</c> is set only for a bearer token; a cookie is never readable by scripts.</summary>
public sealed record SessionResult(string Id, string Name, bool IsServerAdmin, DateTimeOffset ExpiresAt, string? Token);

/// <summary>Login and logout of the server (M4-01). Not available in desktop mode, where there are no accounts.</summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/login", Login).WithName("Login").PublicAccess();
        auth.MapPost("/logout", Logout).WithName("Logout").RequireSignIn();
        return api;
    }

    private static Results<Ok<SessionResult>, ProblemHttpResult> Login(
        LoginRequest request, HttpContext http, ErableHostOptions options, AccountService accounts, LoginThrottle throttle)
    {
        if (!accounts.Enabled)
        {
            return Problems.Problem(StatusCodes.Status404NotFound, "auth.unavailable", "This backend has no accounts.");
        }

        var address = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var login = (request.Login ?? string.Empty).Trim().ToLowerInvariant();
        if (throttle.RetryAfterSeconds(login, address) is { } wait)
        {
            http.Response.Headers.RetryAfter = wait.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Problems.Problem(StatusCodes.Status429TooManyRequests, "auth.tooManyAttempts", "Too many failed attempts; try again later.");
        }

        // Same answer for an unknown login, a wrong password and a disabled account: nothing to enumerate.
        if (accounts.Verify(request.Login, request.Password) is not { } account)
        {
            throttle.RecordFailure(login, address);
            return Problems.Problem(StatusCodes.Status401Unauthorized, "auth.invalidCredentials", "Invalid login or password.");
        }

        throttle.Reset(login);
        var issued = accounts.StartSession(account.Id, request.IssueToken ? ServerStore.SessionKindBearer : ServerStore.SessionKindCookie);
        if (!request.IssueToken)
        {
            SessionCookies.Append(http, options, issued.Token, issued.ExpiresAt);
        }

        return TypedResults.Ok(new SessionResult(
            account.Id, account.DisplayName, account.IsServerAdmin, issued.ExpiresAt, request.IssueToken ? issued.Token : null));
    }

    private static NoContent Logout(HttpContext http, ErableHostOptions options, AccountService accounts)
    {
        if (SessionCookies.TokenOf(http) is { } token)
        {
            accounts.EndSession(token);
        }

        SessionCookies.Delete(http, options);
        return TypedResults.NoContent();
    }
}
