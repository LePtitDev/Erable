using System.Diagnostics;
using Erable.Server.Native;
using Photino.NET;

namespace Erable.Desktop;

/// <summary>
/// Everything that depends on Photino lives here, so that the shell can be swapped (PhotinoX, Electron…) by
/// rewriting this file only (decision Q6, task M0-08).
/// </summary>
internal sealed class PhotinoShell : INativeBridge
{
    private PhotinoWindow? _window;

    /// <summary>Opens the window on <paramref name="url"/> and blocks the (STA) thread until it is closed.</summary>
    public void Run(Uri url)
    {
        _window = new PhotinoWindow()
            .SetTitle("Erable")
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 820)
            .SetMinSize(720, 480)
            .Center()
            // Photino logs loaded URLs at higher verbosity; the URL fragment carries the session token.
            .SetLogVerbosity(0)
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(IsDevToolsEnabled())
            .Load(url);
        _window.WaitForClose();
        _window = null;
    }

    /// <summary>Closes the window from any thread; <see cref="Run"/> then returns.</summary>
    public void Close()
    {
        var window = _window;
        window?.Invoke(window.Close);
    }

    public async Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken)
    {
        var window = _window ?? throw new InvalidOperationException("The window is not open.");
        var folders = await window.ShowOpenFolderAsync(title, null, multiSelect: false).ConfigureAwait(false);
        return folders is [var folder, ..] ? folder : null;
    }

    public Task OpenWithSystemAsync(string path, CancellationToken cancellationToken)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();
        return Task.CompletedTask;
    }

    public Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken)
    {
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("explorer.exe", ["/select,", path])
            : OperatingSystem.IsMacOS()
                ? new ProcessStartInfo("open", ["-R", path])
                : new ProcessStartInfo("xdg-open", [Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path]);
        Process.Start(start)?.Dispose();
        return Task.CompletedTask;
    }

    private static bool IsDevToolsEnabled()
    {
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable("ERABLE_DEVTOOLS") == "1";
#endif
    }
}
