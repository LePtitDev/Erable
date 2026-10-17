using System.Text.Json;
using Velopack;
using Velopack.Sources;

namespace Erable.Desktop;

/// <summary>
/// Checks for a new version at startup (M5-03): when the application was installed with Velopack, the update is
/// downloaded in the background and applied the next time the application quits. Nothing is installed in the middle
/// of a session, and a development build (not installed) never checks.
/// </summary>
internal static class AppUpdater
{
    /// <summary>Set to <c>1</c> to never check for updates (managed deployments).</summary>
    public const string DisableVariable = "ERABLE_DISABLE_UPDATES";

    /// <summary>Per-user setting file of the profile folder: <c>{ "checkForUpdates": false }</c>.</summary>
    public const string SettingsFile = "desktop.json";

    /// <summary>
    /// A folder or an URL holding the files of <c>vpk pack</c>, instead of the GitHub releases: how an update from
    /// version N to N+1 is checked without publishing anything (docs/release.md).
    /// </summary>
    public const string SourceVariable = "ERABLE_UPDATE_SOURCE";

    public const string RepositoryUrl = "https://github.com/LePtitDev/Erable";

    private static readonly JsonSerializerOptions SettingsJson = new(JsonSerializerDefaults.Web);

    private sealed record Settings(bool CheckForUpdates = true);

    /// <summary>False when the environment or the user's settings turn the check off.</summary>
    public static bool IsEnabled(string profileDirectory)
    {
        if (Environment.GetEnvironmentVariable(DisableVariable) == "1")
        {
            return false;
        }

        try
        {
            var path = Path.Combine(profileDirectory, SettingsFile);
            if (File.Exists(path))
            {
                var settings = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), SettingsJson);
                return settings?.CheckForUpdates ?? true;
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable setting file must not stop the application: updates stay on.
        }

        return true;
    }

    private static IUpdateSource CreateSource() =>
        Environment.GetEnvironmentVariable(SourceVariable) is { Length: > 0 } custom
            ? Directory.Exists(custom) ? new SimpleFileSource(new DirectoryInfo(custom)) : new SimpleWebSource(custom)
            : new GithubSource(RepositoryUrl, accessToken: null, prerelease: false);

    /// <summary>Looks for an update and prepares it; never throws (the network may be down).</summary>
    public static async Task CheckAsync(string profileDirectory)
    {
        try
        {
            if (!IsEnabled(profileDirectory))
            {
                return;
            }

            var manager = new UpdateManager(CreateSource());
            if (!manager.IsInstalled)
            {
                return;
            }

            var update = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
            {
                return;
            }

            await manager.DownloadUpdatesAsync(update).ConfigureAwait(false);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Update check failed: {e.Message}");
        }
    }
}
