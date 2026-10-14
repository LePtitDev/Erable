namespace Erable.Server.Native;

/// <summary>
/// The few OS features the SPA needs (docs/architecture.md §10). Implemented by the desktop shell; the SPA only
/// reaches them through the API (endpoints arrive with M1-17), never directly.
/// </summary>
public interface INativeBridge
{
    /// <summary>Shows the OS folder picker; null when the user cancels.</summary>
    Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken);

    /// <summary>Opens a file with its default application.</summary>
    Task OpenWithSystemAsync(string path, CancellationToken cancellationToken);

    /// <summary>Shows a file or folder in the OS file manager.</summary>
    Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken);
}

/// <summary>Server mode: there is no local user session to show native UI to.</summary>
internal sealed class UnavailableNativeBridge : INativeBridge
{
    public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native features are only available in the desktop application.");

    public Task OpenWithSystemAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native features are only available in the desktop application.");

    public Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Native features are only available in the desktop application.");
}
