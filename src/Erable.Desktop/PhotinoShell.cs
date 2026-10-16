using System.Diagnostics;
using System.Runtime.InteropServices;
using Erable.Server.Native;
using Photino.NET;

namespace Erable.Desktop;

/// <summary>
/// Everything that depends on Photino (the PhotinoX fork, decision Q6) lives here, so that the shell can be swapped
/// (Electron…) by rewriting this file only.
/// </summary>
internal sealed partial class PhotinoShell : INativeBridge
{
    private const string DefaultTitle = "Erable";

    private PhotinoWindow? _window;
    private string _title = DefaultTitle;

    /// <summary>Asked when the user closes the window: true keeps it open (unsaved changes, F16.R3).</summary>
    public Func<bool>? Closing { get; set; }

    /// <summary>
    /// A folder dropped on the window (E2E-16-07). The WebView never tells the page the path of a dropped folder; left
    /// unhandled, the drop makes it navigate to <c>file:///…</c>, which the shell cancels and reports here instead.
    /// </summary>
    public Action<string>? FolderDropped { get; set; }

    /// <summary>Test mode only: the folder « chosen » in the picker, which a test cannot click (E2E-16-03).</summary>
    public string? PickFolderOverride { get; set; }

    public bool IsAvailable => true;

    /// <summary>Opens the window on <paramref name="url"/> and blocks the (STA) thread until it is closed.</summary>
    public void Run(Uri url)
    {
        _window = new PhotinoWindow(null)
            // First: Photino logs every setting call, and the URL fragment carries the session token.
            .SetLogVerbosity(0)
            .SetTitle(_title)
            .SetUseOsDefaultSize(false)
            .SetSize(1280, 820)
            .SetMinSize(720, 480)
            .Center()
            .SetContextMenuEnabled(false)
            .SetDevToolsEnabled(IsDevToolsEnabled())
            .RegisterClosingHandler((_, e) => e.Cancel = Closing?.Invoke() ?? false)
            .RegisterNavigationStartingHandler((_, e) => OnNavigationStarting(e));
        if (IconFile() is { } icon)
        {
            _window.SetIconFile(icon);
        }

        if (OperatingSystem.IsWindows())
        {
            // The window only ever talks to the in-process backend on 127.0.0.1. Without this, WebView2 resolves the
            // system proxy (WPAD auto-detection) before the first request, which can stall the empty window for ~20 s.
            _window.SetBrowserControlInitParameters("--no-proxy-server");
        }

        _window.Load(url);
        new PhotinoApplication().Run(_window);
        _window = null;
    }

    /// <summary>The SPA never leaves its origin for a local file: a dropped folder opens as a project, anything else is ignored.</summary>
    private void OnNavigationStarting(NavigationStartingEventArgs e)
    {
        if (!e.Uri.IsFile)
        {
            return;
        }

        e.Cancel = true;
        var path = e.Uri.LocalPath;
        if (Directory.Exists(path))
        {
            FolderDropped?.Invoke(Path.GetFullPath(path));
        }
    }

    /// <summary>Closes the window from any thread; <see cref="Run"/> then returns.</summary>
    public void Close()
    {
        var window = _window;
        window?.Invoke(window.Close);
    }

    public void CloseWindow() => Close();

    public void SetWindowTitle(string title)
    {
        _title = title;
        var window = _window;
        window?.Invoke(() => window.SetTitle(title));
    }

    public void BringToFront()
    {
        var window = _window;
        window?.Invoke(() =>
        {
            if (OperatingSystem.IsWindows())
            {
                var handle = window.WindowHandle;
                if (IsIconic(handle))
                {
                    ShowWindow(handle, ShowWindowRestore);
                }

                SetForegroundWindow(handle);
            }
            else
            {
                window.SetMinimized(false);
                // Raises the window above the others without keeping it there.
                window.SetTopmost(true);
                window.SetTopmost(false);
            }
        });
    }

    public void AllowForeground(int processId)
    {
        if (OperatingSystem.IsWindows())
        {
            AllowSetForegroundWindow(processId);
        }
    }

    public async Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken)
    {
        if (PickFolderOverride is { } chosen)
        {
            return chosen;
        }

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

    /// <summary>Provisional application icon, next to the executable (Windows wants an .ico).</summary>
    private static string? IconFile()
    {
        var path = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "erable.ico" : "erable.png");
        return File.Exists(path) ? path : null;
    }

    private static bool IsDevToolsEnabled()
    {
#if DEBUG
        return true;
#else
        return Environment.GetEnvironmentVariable("ERABLE_DEVTOOLS") == "1";
#endif
    }

    private const int ShowWindowRestore = 9;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr window, int command);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool IsIconic(IntPtr window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int processId);
}
