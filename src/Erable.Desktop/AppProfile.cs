namespace Erable.Desktop;

/// <summary>Per-user application folder: recent projects, preferences (docs/architecture.md §7.1).</summary>
internal static class AppProfile
{
    public static string GetDirectory()
    {
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
