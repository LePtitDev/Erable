using Erable.Core;
using Erable.Core.Accounts;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Security;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Erable.Server.Servers;

/// <summary>A server in the recent list of the home screen, with the mode « Serveur » (E2E-01-06).</summary>
public sealed record ServerItem(
    string Id, string Name, string Url, string? UserId, string? UserName, bool Connected, DateTimeOffset LastUsedAt, IReadOnlyList<ElementId> ProjectIds);

/// <param name="TokensSurviveRestart">False on a system without a vault: the user signs in again at each launch.</param>
public sealed record ServerList(IReadOnlyList<ServerItem> Servers, bool TokensSurviveRestart);

public sealed record ProbeServerRequest(string Url);

public sealed record ConnectServerRequest(string Url, string Login, string Password);

/// <summary>
/// The servers of the desktop application (M4-07). Only the desktop backend has them; mapped everywhere so that the API
/// document describes them, answered with 404 on a server.
/// </summary>
public static class ServersEndpoints
{
    private const string Unavailable = "servers.unavailable";

    public static IEndpointRouteBuilder MapServersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var servers = endpoints.MapGroup(ApiRoutes.V1 + "/servers").WithTags("Servers").RequireSignIn();
        servers.MapGet("/", List).WithName("ListServers");
        servers.MapPost("/probe", Probe).WithName("ProbeServer");
        servers.MapPost("/connect", Connect).WithName("ConnectServer");
        servers.MapGet("/{serverId}/projects", Projects).WithName("ListServerProjects");
        servers.MapDelete("/{serverId}", Forget).WithName("ForgetServer");
        return endpoints;
    }

    private static ProblemHttpResult NotDesktop() =>
        Problems.Problem(StatusCodes.Status404NotFound, Unavailable, "Servers are connected to from the desktop application.");

    private static Results<Ok<ServerList>, ProblemHttpResult> List(ErableHostOptions options, IServiceProvider services)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return NotDesktop();
        }

        var registry = services.GetRequiredService<ServerRegistry>();
        var servers = services.GetRequiredService<RemoteServers>();
        return TypedResults.Ok(new ServerList([.. registry.List().Select(s => Item(s, servers))], servers.TokensSurviveRestart));
    }

    private static async Task<Results<Ok<ServerProbe>, ProblemHttpResult>> Probe(
        ProbeServerRequest request, ErableHostOptions options, IServiceProvider services, HttpContext http)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return NotDesktop();
        }

        var probe = await services.GetRequiredService<RemoteServers>().ProbeAsync(request.Url, http.RequestAborted).ConfigureAwait(false);
        return probe.IsSuccess ? TypedResults.Ok(probe.Value) : Problems.From(probe.Error);
    }

    private static async Task<Results<Created<ServerItem>, ProblemHttpResult>> Connect(
        ConnectServerRequest request, ErableHostOptions options, IServiceProvider services, HttpContext http)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return NotDesktop();
        }

        var servers = services.GetRequiredService<RemoteServers>();
        var connected = await servers.ConnectAsync(request.Url, request.Login, request.Password, http.RequestAborted).ConfigureAwait(false);
        return connected.IsSuccess
            ? TypedResults.Created($"{ApiRoutes.V1}/servers/{connected.Value.Id}", Item(connected.Value, servers))
            : Problems.From(connected.Error);
    }

    private static async Task<Results<Ok<RemoteProject[]>, ProblemHttpResult>> Projects(
        string serverId, ErableHostOptions options, IServiceProvider services, HttpContext http)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return NotDesktop();
        }

        var projects = await services.GetRequiredService<RemoteServers>().ProjectsAsync(serverId, http.RequestAborted).ConfigureAwait(false);
        return projects.IsSuccess ? TypedResults.Ok(projects.Value.ToArray()) : Problems.From(projects.Error);
    }

    private static async Task<Results<NoContent, ProblemHttpResult>> Forget(
        string serverId, ErableHostOptions options, IServiceProvider services, HttpContext http)
    {
        if (options.Mode != HostMode.Desktop)
        {
            return NotDesktop();
        }

        if (services.GetRequiredService<ServerRegistry>().Find(serverId) is null)
        {
            return Problems.From(new DomainError("server.notFound", "Unknown server."));
        }

        await services.GetRequiredService<RemoteServers>().ForgetAsync(serverId, http.RequestAborted).ConfigureAwait(false);
        return TypedResults.NoContent();
    }

    private static ServerItem Item(SavedServer s, RemoteServers servers) =>
        new(s.Id, s.Name, s.Url, s.UserId, s.UserName, servers.IsConnected(s.Id), s.LastUsedAt, s.ProjectIds);
}
