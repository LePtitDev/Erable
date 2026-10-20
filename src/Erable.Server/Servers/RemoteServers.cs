using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Core.Servers;
using Erable.Server.Api;

namespace Erable.Server.Servers;

/// <summary>What the application learned about a server before signing in (F16.R4): its versions and ours.</summary>
public sealed record ServerProbe(
    string Url,
    string Host,
    bool Compatible,
    string ServerAppVersion,
    string ServerApiVersion,
    int ServerFormatVersion,
    string AppVersion,
    string AppApiVersion,
    int AppFormatVersion);

/// <summary>A project of a server the user belongs to.</summary>
public sealed record RemoteProject(ElementId Id, string Name, string ContentLanguage, ProjectRole MyRole, string ServerId);

/// <summary>
/// The desktop application as a client of a server (F01 E2E-01-06, F16.R4): checks the versions before anything
/// else, signs in, keeps the bearer token in the vault of the system (never in the page) and lists the projects.
/// </summary>
public sealed class RemoteServers(ServerRegistry registry, ISecretStore secrets, TimeProvider clock)
{
    public const string Unreachable = "server.unreachable";
    public const string NotErable = "server.notErable";
    public const string Incompatible = "server.incompatible";
    public const string InvalidUrl = ServerAddress.InvalidUrl;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        ConnectTimeout = TimeSpan.FromSeconds(6),
    })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    /// <summary>A bearer token of the session in memory, for a system without a vault: signing in again is for the next launch.</summary>
    private readonly MemorySecretStore _session = new();

    public bool TokensSurviveRestart => secrets.IsPersistent;

    /// <summary>The origin of an address typed by a person (<c>https://wiki.example.org/</c> → <c>https://wiki.example.org</c>).</summary>
    public static Result<string> Normalize(string? address) => ServerAddress.Normalize(address);

    /// <summary><c>GET /api/info</c> and nothing else: the first and, for an incompatible server, the only request (F16.R4).</summary>
    public async Task<Result<ServerProbe>> ProbeAsync(string? address, CancellationToken cancellationToken)
    {
        var url = Normalize(address);
        if (!url.IsSuccess)
        {
            return url.Error;
        }

        ServerHandshake? info;
        try
        {
            using var response = await Http.GetAsync(new Uri(url.Value + "/api/info"), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new DomainError(NotErable, $"{url.Value} answered {(int)response.StatusCode}: it does not look like an Erable server.");
            }

            // Read by hand: a newer server may add or change fields (a mode this version does not know…).
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false), cancellationToken: cancellationToken).ConfigureAwait(false);
            info = ServerHandshake.Parse(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return ex is JsonException or NotSupportedException
                ? new DomainError(NotErable, $"{url.Value} does not look like an Erable server.")
                : new DomainError(Unreachable, $"{url.Value} cannot be reached: {ex.Message}");
        }

        if (info is null)
        {
            return new DomainError(NotErable, $"{url.Value} does not look like an Erable server.");
        }

        var host = new Uri(url.Value).Authority;
        return new ServerProbe(
            url.Value, host, info.ApiVersion == InfoEndpoints.ApiVersion, info.AppVersion, info.ApiVersion, info.FormatVersion,
            InfoEndpoints.AppVersion, InfoEndpoints.ApiVersion, Erable.Core.ProjectFormat.CurrentVersion);
    }

    /// <summary>Probes, then signs in with <c>issueToken</c>, keeps the token and the server, in that order.</summary>
    public async Task<Result<SavedServer>> ConnectAsync(string? address, string? login, string? password, CancellationToken cancellationToken)
    {
        var probe = await ProbeAsync(address, cancellationToken).ConfigureAwait(false);
        if (!probe.IsSuccess)
        {
            return probe.Error;
        }

        if (!probe.Value.Compatible)
        {
            return IncompatibleError(probe.Value);
        }

        SessionResult? session;
        try
        {
            using var response = await Http.PostAsJsonAsync(
                new Uri(probe.Value.Url + "/api/v1/auth/login"), new LoginRequest(login ?? string.Empty, password ?? string.Empty, IssueToken: true),
                Json, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return new DomainError("auth.tooManyAttempts", "Too many failed attempts; try again later.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new DomainError("auth.invalidCredentials", "Invalid login or password.");
            }

            session = await response.Content.ReadFromJsonAsync<SessionResult>(Json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new DomainError(Unreachable, $"{probe.Value.Url} cannot be reached: {ex.Message}");
        }

        if (session?.Token is not { Length: > 0 } token)
        {
            return new DomainError(NotErable, $"{probe.Value.Url} did not give a session token.");
        }

        var id = ServerRegistry.IdOf(probe.Value.Url);
        // The vault of the system when there is one; in memory otherwise (and when the vault refuses).
        if (!secrets.Write(ServerRegistry.TokenKey(id), token))
        {
            _session.Write(ServerRegistry.TokenKey(id), token);
        }

        return registry.Upsert(probe.Value.Url, probe.Value.Host, session.Id, session.Name, clock.GetUtcNow());
    }

    public string? TokenOf(string serverId) =>
        secrets.Read(ServerRegistry.TokenKey(serverId)) ?? _session.Read(ServerRegistry.TokenKey(serverId));

    public bool IsConnected(string serverId) => TokenOf(serverId) is not null;

    /// <summary>The token is no good any more (the server answered 401): the user signs in again.</summary>
    public void Disconnect(string serverId)
    {
        secrets.Delete(ServerRegistry.TokenKey(serverId));
        _session.Delete(ServerRegistry.TokenKey(serverId));
    }

    /// <summary>Forgets a server; the session on the server is ended too, if it still answers.</summary>
    public async Task ForgetAsync(string serverId, CancellationToken cancellationToken)
    {
        if (registry.Find(serverId) is { } server && TokenOf(serverId) is { } token)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(server.Url + "/api/v1/auth/logout"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var _ = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // The server is gone or unreachable: the token expires by itself.
            }
        }

        Disconnect(serverId);
        registry.Remove(serverId);
    }

    /// <summary>The projects the signed-in user belongs to on a server; they become reachable through the application (<see cref="RemoteProxy"/>).</summary>
    public async Task<Result<IReadOnlyList<RemoteProject>>> ProjectsAsync(string serverId, CancellationToken cancellationToken)
    {
        if (registry.Find(serverId) is not { } server)
        {
            return new DomainError("server.notFound", "Unknown server.");
        }

        if (TokenOf(serverId) is not { } token)
        {
            return new DomainError("auth.required", "Sign in to this server first.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server.Url + "/api/v1/projects"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Disconnect(serverId);
                return new DomainError("auth.required", "The session on this server has ended: sign in again.");
            }

            if (!response.IsSuccessStatusCode)
            {
                return new DomainError(NotErable, $"{server.Url} answered {(int)response.StatusCode}.");
            }

            var projects = await response.Content.ReadFromJsonAsync<ProjectSummary[]>(Json, cancellationToken).ConfigureAwait(false) ?? [];
            registry.SetProjects(serverId, [.. projects.Select(p => p.Id)]);
            return Result.Success<IReadOnlyList<RemoteProject>>(
                [.. projects.Select(p => new RemoteProject(p.Id, p.Name, p.ContentLanguage, p.MyRole, serverId))]);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new DomainError(Unreachable, $"{server.Url} cannot be reached: {ex.Message}");
        }
    }

    public static DomainError IncompatibleError(ServerProbe probe)
    {
        ArgumentNullException.ThrowIfNull(probe);
        return new DomainError(Incompatible, $"{probe.Url} speaks API {probe.ServerApiVersion}; this application speaks {probe.AppApiVersion}.",
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["serverAppVersion"] = probe.ServerAppVersion,
                ["serverApiVersion"] = probe.ServerApiVersion,
                ["serverFormatVersion"] = probe.ServerFormatVersion,
                ["appVersion"] = probe.AppVersion,
                ["appApiVersion"] = probe.AppApiVersion,
                ["appFormatVersion"] = probe.AppFormatVersion,
            });
    }
}
