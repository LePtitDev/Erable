#if ERABLE_TEST_ENDPOINTS
using System.Text.Json;
using Erable.Core.Accounts;
using Erable.Server.Accounts;
using Erable.Server.Hosting;

namespace Erable.Server.Testing;

/// <summary>A user of <c>spec/projects/users.json</c> (docs/features.md §2.3).</summary>
internal sealed record TestUser(string Id, string Name, ProjectRole? Role, bool ServerAdmin, string Password);

/// <summary>The test users, created as accounts when a server starts in test mode.</summary>
internal static class TestUsers
{
    public static IReadOnlyList<TestUser> Read(ErableHostOptions options)
    {
        var path = options.FixturesDirectory is { } fixtures ? Path.Combine(fixtures, "users.json") : null;
        if (path is null || !File.Exists(path))
        {
            return [];
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var users = new List<TestUser>();
        foreach (var user in document.RootElement.GetProperty("users").EnumerateArray())
        {
            var role = user.TryGetProperty("role", out var r) && r.ValueKind == JsonValueKind.String && ProjectRoles.TryParse(r.GetString(), out var parsed)
                ? parsed
                : (ProjectRole?)null;
            users.Add(new TestUser(
                user.GetProperty("id").GetString()!,
                user.GetProperty("name").GetString()!,
                role,
                user.TryGetProperty("serverAdmin", out var admin) && admin.ValueKind == JsonValueKind.True,
                user.GetProperty("password").GetString()!));
        }

        return users;
    }

    /// <summary>Creates the missing accounts.</summary>
    public static void Seed(ErableHostOptions options, AccountService accounts)
    {
        foreach (var user in Read(options))
        {
            if (accounts.Store.GetAccount(user.Id) is null)
            {
                accounts.CreateAccount(user.Id, user.Name, user.Password, user.ServerAdmin);
            }
        }
    }
}
#endif
