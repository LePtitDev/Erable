using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Core.Servers;

namespace Erable.Mobile.Shell;

/// <summary>A server the user added (F19.R1): its origin and what its handshake said when it was last checked.</summary>
public sealed record SavedServer(string Origin, string AppVersion, DateTimeOffset CheckedAt)
{
    /// <summary>What the list shows: the host and port, without the scheme.</summary>
    [JsonIgnore]
    public string Host => new Uri(Origin).Authority;

    /// <summary>Plain HTTP, accepted for a server of the local network but pointed out (F19.R3).</summary>
    [JsonIgnore]
    public bool IsCleartext => ServerAddress.IsCleartext(Origin);
}

/// <summary>
/// The servers of the application, most recently checked first, one per origin. Stored as JSON in the preferences of
/// the application; an unreadable value gives an empty list rather than a crash at launch.
/// </summary>
public sealed class ServerList
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly List<SavedServer> _servers;

    private ServerList(List<SavedServer> servers)
    {
        _servers = servers;
    }

    public IReadOnlyList<SavedServer> Servers => _servers;

    public static ServerList Empty() => new([]);

    public static ServerList Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Empty();
        }

        try
        {
            var stored = JsonSerializer.Deserialize<StoredList>(json, Json);
            var servers = (stored?.Servers ?? [])
                .Where(s => s is not null && ServerAddress.Normalize(s.Origin) is { IsSuccess: true } n && n.Value == s.Origin)
                .DistinctBy(s => s.Origin, StringComparer.Ordinal)
                .ToList();
            return new ServerList(servers);
        }
        catch (JsonException)
        {
            return Empty();
        }
    }

    public string Write() => JsonSerializer.Serialize(new StoredList(StoredList.CurrentVersion, _servers), Json);

    /// <summary>Adds the server, or brings an origin already known to the top with its new handshake.</summary>
    public ServerList Add(SavedServer server)
    {
        ArgumentNullException.ThrowIfNull(server);
        return new ServerList([server, .. _servers.Where(s => s.Origin != server.Origin)]);
    }

    public ServerList Remove(string origin) => new([.. _servers.Where(s => s.Origin != origin)]);

    private sealed record StoredList(
        [property: JsonPropertyName("version")] int Version,
        [property: JsonPropertyName("servers")] IReadOnlyList<SavedServer>? Servers)
    {
        public const int CurrentVersion = 1;
    }
}

/// <summary>Why a server cannot be added (F19.R1): stable codes, translated by the application.</summary>
public static class ServerErrors
{
    public const string InvalidUrl = ServerAddress.InvalidUrl;
    public const string Unreachable = "server.unreachable";
    public const string NotErable = "server.notErable";

    /// <summary>The loopback backend of a desktop application, never a server to share.</summary>
    public const string NotServerMode = "server.notServerMode";

    /// <summary>
    /// The verdict on the answer of <c>GET /api/info</c>: an Erable server in server mode, or why not. The version of
    /// its API does not matter: the SPA shown is the server's own.
    /// </summary>
    public static Result<ServerHandshake> Check(string origin, int status, string body)
    {
        if (status is < 200 or > 299)
        {
            return new DomainError(NotErable, $"{origin} answered {status}: it does not look like an Erable server.");
        }

        var handshake = ServerHandshake.Parse(body);
        if (handshake is null)
        {
            return new DomainError(NotErable, $"{origin} does not look like an Erable server.");
        }

        return handshake.IsServerMode
            ? handshake
            : new DomainError(NotServerMode, $"{origin} is an Erable application, not a server (mode {handshake.Mode ?? "?"}).");
    }
}
