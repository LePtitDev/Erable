using System.Security.Cryptography;
using System.Text;
using Erable.Core;
using Erable.Server.Projects;

namespace Erable.Server.Servers;

/// <summary>A server the desktop application connected to (F01.R2: the recent servers live in the user profile).</summary>
/// <param name="Id">16 hexadecimal characters derived from the address: connecting twice to a server updates one entry.</param>
/// <param name="Url">Origin of the server (<c>https://wiki.example.org</c>).</param>
/// <param name="ProjectIds">The projects the server listed last: requests for them go to the server.</param>
public sealed record SavedServer(
    string Id, string Url, string Name, string? UserId, string? UserName, DateTimeOffset LastUsedAt, IReadOnlyList<ElementId> ProjectIds);

/// <summary>
/// The servers of the profile and which project belongs to which. No secret here: the token is in the
/// <see cref="ISecretStore"/>.
/// </summary>
public sealed class ServerRegistry(UserProfiles profiles)
{
    private const string FileName = "servers.json";

    private readonly Lock _gate = new();

    private UserProfile Profile => profiles.Default;

    public IReadOnlyList<SavedServer> List()
    {
        lock (_gate)
        {
            return [.. Load().OrderByDescending(s => s.LastUsedAt)];
        }
    }

    public SavedServer? Find(string id)
    {
        lock (_gate)
        {
            return Load().FirstOrDefault(s => s.Id == id);
        }
    }

    /// <summary>The server that hosts a project, if the project is one of a server.</summary>
    public SavedServer? ServerOfProject(ElementId projectId)
    {
        lock (_gate)
        {
            return Load().FirstOrDefault(s => s.ProjectIds.Contains(projectId));
        }
    }

    public SavedServer Upsert(string url, string name, string? userId, string? userName, DateTimeOffset at)
    {
        lock (_gate)
        {
            var servers = Load();
            var id = IdOf(url);
            var known = servers.FirstOrDefault(s => s.Id == id);
            var saved = new SavedServer(id, url, name, userId, userName, at, known?.ProjectIds ?? []);
            Save([.. servers.Where(s => s.Id != id), saved]);
            return saved;
        }
    }

    /// <summary>Replaces the projects known for a server (what it just listed).</summary>
    public void SetProjects(string id, IReadOnlyList<ElementId> projectIds)
    {
        lock (_gate)
        {
            var servers = Load();
            if (servers.FirstOrDefault(s => s.Id == id) is { } server)
            {
                Save([.. servers.Where(s => s.Id != id), server with { ProjectIds = projectIds }]);
            }
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            var servers = Load();
            if (servers.All(s => s.Id != id))
            {
                return false;
            }

            Save([.. servers.Where(s => s.Id != id)]);
            return true;
        }
    }

    public static string IdOf(string url) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.ToLowerInvariant())))[..ElementId.Length];

    /// <summary>The secret key of the token of a server.</summary>
    public static string TokenKey(string id) => "server-" + id;

    private List<SavedServer> Load() => Profile.Read<List<SavedServer>>(FileName) ?? [];

    private void Save(IEnumerable<SavedServer> servers) => Profile.Write(FileName, servers.ToList());
}
