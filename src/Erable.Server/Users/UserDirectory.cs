using System.Text.Json;
using Erable.Core.Projects;
using Erable.Server.Hosting;
using Erable.Server.Projects;

namespace Erable.Server.Users;

/// <summary>
/// Display names of the authors recorded in projects (<c>createdBy</c>, <c>updatedBy</c>): the accounts of the server, else
/// the local user, under the name chosen in the preferences or, by default, the OS account name; test mode also knows
/// the users of <c>spec/projects/users.json</c>, the authors of the reference project. An unknown author is shown by its identifier.
/// </summary>
public sealed class UserDirectory
{
    private readonly Dictionary<string, string> _names = new(StringComparer.Ordinal);
    private readonly Accounts.AccountService _accounts;
    private readonly UserProfiles _profiles;
    private readonly IHttpContextAccessor _http;

    public UserDirectory(ErableHostOptions options, Accounts.AccountService accounts, UserProfiles profiles, IHttpContextAccessor http)
    {
        ArgumentNullException.ThrowIfNull(options);
        _accounts = accounts;
        _profiles = profiles;
        _http = http;
        _names[KnownUsers.Local] = LocalName();
        if (ErableHost.TestEndpointsCompiled && options.TestMode && options.FixturesDirectory is { } fixtures)
        {
            foreach (var (id, name) in ReadUsers(Path.Combine(fixtures, "users.json")))
            {
                _names[id] = name;
            }
        }
    }

    /// <summary>Display name of a user ID; the ID itself when unknown.</summary>
    public string NameOf(string userId)
    {
        ArgumentNullException.ThrowIfNull(userId);
        if (userId == KnownUsers.Local && _profiles.For(_http.HttpContext).GetDisplayName() is { } chosen)
        {
            return chosen;
        }

        return _accounts.DisplayNameOf(userId) ?? (_names.TryGetValue(userId, out var name) ? name : userId);
    }

    /// <summary>The OS account name: a local install has no other identity (« Bonjour, Arthur »).</summary>
    private static string LocalName()
    {
        var name = Environment.UserName;
        return string.IsNullOrWhiteSpace(name) ? KnownUsers.Local : char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static IEnumerable<(string Id, string Name)> ReadUsers(string path)
    {
        if (!File.Exists(path))
        {
            yield break;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (var user in document.RootElement.GetProperty("users").EnumerateArray())
        {
            if (user.GetProperty("id").GetString() is { } id && user.GetProperty("name").GetString() is { } name)
            {
                yield return (id, name);
            }
        }
    }
}
