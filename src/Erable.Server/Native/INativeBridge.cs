namespace Erable.Server.Native;

/// <summary>
/// The few OS features the SPA needs (docs/architecture.md §10). Implemented by the desktop shell; the SPA only
/// reaches them through the API (<see cref="Api.NativeEndpoints"/>), never directly.
/// </summary>
public interface INativeBridge
{
    /// <summary>False in server mode: there is no local user session to show native UI to.</summary>
    bool IsAvailable { get; }

    /// <summary>Shows the OS folder picker; null when the user cancels.</summary>
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken);

    /// <summary>Opens a file with its default application.</summary>
    Task OpenWithSystemAsync(string path, CancellationToken cancellationToken);

    /// <summary>Shows a file or folder in the OS file manager.</summary>
    Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken);

    /// <summary>Title of the window (the current project, then « Erable »).</summary>
    void SetWindowTitle(string title);

    /// <summary>Restores the window if minimized and brings it to the foreground (F16.R2).</summary>
    void BringToFront();

    /// <summary>
    /// Lets another process take the foreground (Windows only lets the foreground process give it away): called
    /// before asking another instance to come to the front.
    /// </summary>
    void AllowForeground(int processId);

    /// <summary>Closes the window for good (the user has answered the unsaved changes question, F16.R3).</summary>
    void CloseWindow();
}

/// <summary>Server mode: there is no local user session to show native UI to.</summary>
internal sealed class UnavailableNativeBridge : INativeBridge
{
    public bool IsAvailable => false;

    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken) => throw Unavailable();

    public Task OpenWithSystemAsync(string path, CancellationToken cancellationToken) => throw Unavailable();

    public Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken) => throw Unavailable();

    public void SetWindowTitle(string title) => throw Unavailable();

    public void BringToFront() => throw Unavailable();

    public void AllowForeground(int processId)
    {
        // Nothing to give away: a server has no window.
    }

    public void CloseWindow() => throw Unavailable();

    private static NotSupportedException Unavailable() =>
        new("Native features are only available in the desktop application.");
}
