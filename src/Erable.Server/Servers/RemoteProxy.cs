using System.Text.RegularExpressions;
using Erable.Core;
using Erable.Server.Api;
using Erable.Server.Security;
using Yarp.ReverseProxy.Forwarder;

namespace Erable.Server.Servers;

/// <summary>
/// The requests of a project that lives on a server go to that server (M4-07). The page keeps talking to its own
/// origin — <c>/api/v1/projects/&lt;id&gt;/…</c> and <c>/hubs/project?projectId=&lt;id&gt;</c> — and the desktop backend
/// forwards them with the bearer token, which therefore never reaches the page. Images, videos and downloads work
/// without any change, since the browser asks the local origin for them; no CORS is involved.
/// </summary>
internal sealed partial class RemoteProxyMiddleware(
    RequestDelegate next, ServerRegistry registry, RemoteServers servers, IHttpForwarder forwarder)
{
    /// <summary>Header added to every forwarded answer: the page learns which server a 401 comes from.</summary>
    public const string ServerHeader = RemoteProxyHeaders.Server;

    private static readonly HttpMessageInvoker Invoker = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        AutomaticDecompression = System.Net.DecompressionMethods.None,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(10),
    });

    public async Task InvokeAsync(HttpContext context)
    {
        if (ServerOf(context) is not { } server)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Response.Headers[ServerHeader] = server.Id;
        if (servers.TokenOf(server.Id) is not { } token)
        {
            await Problems.Problem(StatusCodes.Status401Unauthorized, "auth.required", "Sign in to this server first.")
                .ExecuteAsync(context).ConfigureAwait(false);
            return;
        }

        var error = await forwarder.SendAsync(context, server.Url, Invoker, ForwarderRequestConfig.Empty, new RemoteTransformer(server.Id, token, servers))
            .ConfigureAwait(false);
        if (error != ForwarderError.None && !context.Response.HasStarted)
        {
            await Problems.Problem(StatusCodes.Status502BadGateway, RemoteServers.Unreachable, $"{server.Url} cannot be reached.")
                .ExecuteAsync(context).ConfigureAwait(false);
        }
    }

    private SavedServer? ServerOf(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        string? id = null;
        if (ProjectPath().Match(path) is { Success: true } match)
        {
            id = match.Groups[1].Value;
        }
        else if (path.StartsWith(ApiRoutes.ProjectHub, StringComparison.OrdinalIgnoreCase))
        {
            id = context.Request.Query["projectId"].ToString();
        }

        return ElementId.TryParse(id, out var projectId) ? registry.ServerOfProject(projectId) : null;
    }

    [GeneratedRegex("^/api/v1/projects/([0-9A-Fa-f]{16})(?:/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex ProjectPath();

    private sealed class RemoteTransformer(string serverId, string token, RemoteServers servers) : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext, HttpRequestMessage proxyRequest, string destinationPrefix, CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken).ConfigureAwait(false);

            // Nothing of the local session goes to the server, and the server sees a plain API client with its token.
            foreach (var name in new[]
            {
                "Cookie", "Origin", "Referer", "Authorization", DesktopSession.TokenHeader, "Sec-Fetch-Site", "Sec-Fetch-Mode", "Sec-Fetch-Dest",
            })
            {
                proxyRequest.Headers.Remove(name);
            }

            proxyRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        public override async ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext, HttpResponseMessage? proxyResponse, CancellationToken cancellationToken)
        {
            var handled = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken).ConfigureAwait(false);
            // A cookie of the server would land on the local origin, next to the one of the desktop session.
            httpContext.Response.Headers.Remove("Set-Cookie");
            httpContext.Response.Headers[ServerHeader] = serverId;
            if (proxyResponse?.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // The token is no good any more: the home screen shows the server as disconnected.
                servers.Disconnect(serverId);
            }

            return handled;
        }
    }
}

/// <summary>Names the page and the tests rely on.</summary>
public static class RemoteProxyHeaders
{
    /// <summary>Added to every forwarded answer: which server it comes from.</summary>
    public const string Server = "X-Erable-Remote-Server";
}
