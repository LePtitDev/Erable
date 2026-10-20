using System.Text.Json;
using Erable.Core;
using Erable.Core.Accounts;
using Erable.Storage;

namespace Erable.Server.Projects;

/// <summary>A project in the recent list (F01.R2).</summary>
public sealed record RecentProject(ElementId ProjectId, string Name, string Folder, DateTimeOffset LastOpenedAt, ElementId? LastElementId);

/// <summary>
/// Per-user data that does not belong to a project (docs/architecture.md §7.1, decision Q7): stored in the
/// application profile folder as small JSON documents. Server mode moves it to <c>server.db</c> with accounts (M4).
/// </summary>
public sealed class UserProfile
{
    public const int MaxRecentProjects = 20;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, NewLine = "\n" };

    private const string IdentityFile = "identity.json";

    private readonly Lock _gate = new();
    private readonly string _directory;
    private string? _displayName;
    private bool _identityLoaded;

    public UserProfile(string directory)
    {
        _directory = directory;
    }

    /// <summary>Folder of the profile documents.</summary>
    public string Directory => _directory;

    public IReadOnlyList<RecentProject> GetRecentProjects()
    {
        lock (_gate)
        {
            return Read<List<RecentProject>>("recent-projects.json") ?? [];
        }
    }

    /// <summary>Puts the project first in the recent list.</summary>
    public void RecordOpened(OpenedProject project, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(project);
        lock (_gate)
        {
            var list = Read<List<RecentProject>>("recent-projects.json") ?? [];
            var previous = list.FirstOrDefault(r => r.ProjectId == project.Manifest.ProjectId);
            list.RemoveAll(r => r.ProjectId == project.Manifest.ProjectId || SameFolder(r.Folder, project.Folder));
            list.Insert(0, new RecentProject(project.Manifest.ProjectId, project.Manifest.Name, project.Folder, at, previous?.LastElementId));
            Write("recent-projects.json", list.Take(MaxRecentProjects).ToList());
        }
    }

    /// <summary>Remembers the last element viewed in a project (E2E-01-02).</summary>
    public bool RecordLastElement(ElementId projectId, ElementId elementId)
    {
        lock (_gate)
        {
            var list = Read<List<RecentProject>>("recent-projects.json") ?? [];
            var index = list.FindIndex(r => r.ProjectId == projectId);
            if (index < 0)
            {
                return false;
            }

            list[index] = list[index] with { LastElementId = elementId };
            Write("recent-projects.json", list);
            return true;
        }
    }

    /// <summary>Removes a project from the recent list without touching its files (F01.R2).</summary>
    public bool RemoveRecentProject(ElementId projectId)
    {
        lock (_gate)
        {
            var list = Read<List<RecentProject>>("recent-projects.json") ?? [];
            var removed = list.RemoveAll(r => r.ProjectId == projectId) > 0;
            if (removed)
            {
                Write("recent-projects.json", list);
            }

            return removed;
        }
    }

    public UserPreferences GetPreferences()
    {
        lock (_gate)
        {
            return Read<UserPreferences>("preferences.json")?.Normalize() ?? UserPreferences.Default;
        }
    }

    public void SavePreferences(UserPreferences preferences)
    {
        lock (_gate)
        {
            Write("preferences.json", preferences.Normalize());
        }
    }

    /// <summary>
    /// Display name chosen by the local user (desktop), or null to keep the OS account name. Kept in memory once read:
    /// it is looked up for every author shown (history, presence).
    /// </summary>
    public string? GetDisplayName()
    {
        lock (_gate)
        {
            if (!_identityLoaded)
            {
                _displayName = AccountRules.NormalizeDisplayName(Read<IdentityDocument>(IdentityFile)?.Name) is { IsSuccess: true } name
                    ? name.Value
                    : null;
                _identityLoaded = true;
            }

            return _displayName;
        }
    }

    /// <summary>Saves the display name of the local user; it must already be normalized (<see cref="AccountRules.NormalizeDisplayName"/>).</summary>
    public void SaveDisplayName(string name)
    {
        lock (_gate)
        {
            Write(IdentityFile, new IdentityDocument(name));
            _displayName = name;
            _identityLoaded = true;
        }
    }

    /// <summary>Reads a profile document; null when absent or unreadable (a damaged profile must not block the app).</summary>
    public T? Read<T>(string name)
        where T : class
    {
        var path = Path.Combine(_directory, name);
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Atomic write of a profile document.</summary>
    public void Write<T>(string name, T value)
    {
        var path = Path.Combine(_directory, name);
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(value, Json));
        File.Move(temporary, path, overwrite: true);
    }

    /// <summary>Deletes a profile document; nothing happens when it does not exist.</summary>
    public void Delete(string name) => File.Delete(Path.Combine(_directory, name));

    private sealed record IdentityDocument(string? Name);

    private static bool SameFolder(string a, string b) =>
        string.Equals(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar),
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
}
