using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Erable.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Api;

/// <summary>Credentials of a login. <c>IssueToken</c> asks for a bearer token instead of a cookie (desktop connected).</summary>
public sealed record LoginRequest(string Login, string Password, bool IssueToken = false);

/// <summary>The session just opened. <c>Token</c> is set only for a bearer token; a cookie is never readable by scripts.</summary>
public sealed record SessionResult(string Id, string Name, bool IsServerAdmin, DateTimeOffset ExpiresAt, string? Token);

/// <summary>A user changing their own password: the current one proves that the session is still theirs.</summary>
public sealed record ChangeMyPasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Login and logout of the server. Not available in desktop mode, where there are no accounts.</summary>
public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");
        auth.MapPost("/login", Login).WithName("Login").PublicAccess();
        auth.MapPost("/logout", Logout).WithName("Logout").RequireSignIn();
        auth.MapPut("/password", ChangeMyPassword).WithName("ChangeMyPassword").RequireSignIn();
        return api;
    }

    private static async Task<Results<Ok<SessionResult>, ProblemHttpResult>> Login(
        LoginRequest request,
        HttpContext http,
        ErableHostOptions options,
        AccountService accounts,
        LoginThrottle throttle,
        ILoggerFactory loggers)
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

        // A login tried too often from everywhere is slowed down, never blocked (its owner must still get in).
        if (throttle.DelayFor(login) is { } delay && delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, options.TimeProvider, http.RequestAborted).ConfigureAwait(false);
        }

        // Same answer for an unknown login, a wrong password and a disabled account: nothing to enumerate.
        if (accounts.Verify(request.Login, request.Password) is not { } account)
        {
            throttle.RecordFailure(login, address);
            if (throttle.RetryAfterSeconds(login, address) is { } blockedFor)
            {
                ServerLog.LoginBlocked(loggers.CreateLogger(typeof(AuthEndpoints)), login, address, blockedFor);
            }

            return Problems.Problem(StatusCodes.Status401Unauthorized, "auth.invalidCredentials", "Invalid login or password.");
        }

        throttle.Reset(login, address);
        var issued = accounts.StartSession(account.Id, request.IssueToken ? ServerStore.SessionKindBearer : ServerStore.SessionKindCookie);
        if (!request.IssueToken)
        {
            SessionCookies.Append(http, options, issued.Token, issued.ExpiresAt);
        }

        return TypedResults.Ok(new SessionResult(
            account.Id, account.DisplayName, account.IsServerAdmin, issued.ExpiresAt, request.IssueToken ? issued.Token : null));
    }

    /// <summary>
    /// Changes the password of the signed-in user (audit S4). Every session of the account ends, as when an administrator
    /// changes it; the device making the request gets a new session of the same kind, so it stays signed in.
    /// Failed checks of the current password count against the brute-force limits of the login.
    /// </summary>
    private static Results<Ok<SessionResult>, ProblemHttpResult> ChangeMyPassword(
        ChangeMyPasswordRequest request, HttpContext http, ErableHostOptions options, AccountService accounts, LoginThrottle throttle)
    {
        if (!accounts.Enabled)
        {
            return Problems.Problem(StatusCodes.Status404NotFound, "auth.unavailable", "This backend has no accounts.");
        }

        var id = CurrentUser.Id(http);
        var address = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        if (throttle.RetryAfterSeconds(id, address) is { } wait)
        {
            http.Response.Headers.RetryAfter = wait.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return Problems.Problem(StatusCodes.Status429TooManyRequests, "auth.tooManyAttempts", "Too many failed attempts; try again later.");
        }

        if (accounts.Verify(id, request.CurrentPassword) is not { } account)
        {
            throttle.RecordFailure(id, address);
            return Problems.Problem(StatusCodes.Status403Forbidden, "auth.wrongCurrentPassword", "The current password is wrong.");
        }

        var changed = accounts.ChangePassword(id, request.NewPassword);
        if (!changed.IsSuccess)
        {
            return Problems.From(changed.Error);
        }

        throttle.Reset(id, address);
        var bearer = http.Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
        var issued = accounts.StartSession(id, bearer ? ServerStore.SessionKindBearer : ServerStore.SessionKindCookie);
        if (!bearer)
        {
            SessionCookies.Append(http, options, issued.Token, issued.ExpiresAt);
        }

        return TypedResults.Ok(new SessionResult(
            account.Id, account.DisplayName, account.IsServerAdmin, issued.ExpiresAt, bearer ? issued.Token : null));
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
