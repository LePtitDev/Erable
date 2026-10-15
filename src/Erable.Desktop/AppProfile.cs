namespace Erable.Desktop;

/// <summary>Per-user application folder: recent projects, preferences (docs/architecture.md §7.1).</summary>
internal static class AppProfile
{
    /// <summary>Another profile folder: desktop tests must never touch the profile of the person running them.</summary>
    public const string OverrideVariable = "ERABLE_PROFILE_DIR";

    public static string GetDirectory()
    {
        if (Environment.GetEnvironmentVariable(OverrideVariable) is { Length: > 0 } custom)
        {
            return Path.GetFullPath(custom);
        }

        string root;
        if (OperatingSystem.IsWindows())
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(root, "Erable");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support", "Erable");
        }

        root = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(home, ".config");
        return Path.Combine(root, "erable");
    }
}
