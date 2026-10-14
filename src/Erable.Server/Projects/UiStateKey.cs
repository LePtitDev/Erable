using System.Text.RegularExpressions;

namespace Erable.Server.Projects;

/// <summary>Keys of the UI state documents of a profile (<c>me/ui-state/{key}</c>).</summary>
public static partial class UiStateKey
{
    /// <summary>Drafts of long pages fit; anything larger is not UI state.</summary>
    public const int MaxBytes = 2 * 1024 * 1024;

    public static bool IsValid(string? key) => key is not null && Pattern().IsMatch(key);

    /// <summary>One file per key, in its own folder of the profile.</summary>
    public static string FileOf(string key) => Path.Combine("ui-state", key + ".json");

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex Pattern();
}
