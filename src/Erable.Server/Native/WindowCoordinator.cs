using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Erable.Server.Native;

/// <summary>Something the desktop shell tells the SPA (server-sent events of <c>GET /api/v1/native/events</c>).</summary>
/// <param name="Type">
/// <c>closeRequested</c>: the user closed the window while a page had unsaved changes; <c>folderDropped</c>: a folder
/// was dropped on the window (E2E-16-07).
/// </param>
/// <param name="Path">The dropped folder (<c>folderDropped</c> only).</param>
public sealed record NativeEvent(string Type, string? Path = null)
{
    public static readonly NativeEvent CloseRequested = new("closeRequested");

    public static NativeEvent FolderDropped(string path) => new("folderDropped", path);
}

/// <summary>
/// Closing the desktop window without losing work (F16.R3). The SPA reports whether it has unsaved changes; when the
/// user closes the window, the shell asks <see cref="OnClosing"/>: without unsaved changes the window closes at once,
/// otherwise it stays open and the SPA, told by a <see cref="NativeEvent.CloseRequested"/> event, asks « Enregistrer /
/// Abandonner / Annuler » and then calls <see cref="ApproveClose"/>. With no SPA listening, nothing can ask: the
/// window closes (the drafts of F05.R4 are already saved).
/// </summary>
public sealed class WindowCoordinator(INativeBridge bridge)
{
    private readonly Lock _gate = new();
    private readonly List<Channel<NativeEvent>> _subscribers = [];
    private bool _unsavedChanges;
    private bool _closeApproved;

    public void SetUnsavedChanges(bool value)
    {
        lock (_gate)
        {
            _unsavedChanges = value;
        }
    }

    /// <summary>Called by the shell (UI thread) when the user closes the window: true keeps it open.</summary>
    public bool OnClosing()
    {
        lock (_gate)
        {
            if (_closeApproved || !_unsavedChanges || _subscribers.Count == 0)
            {
                return false;
            }

            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(NativeEvent.CloseRequested);
            }

            return true;
        }
    }

    /// <summary>Called by the shell when a folder is dropped on the window: the SPA opens it like « Ouvrir un dossier ».</summary>
    public void OnFolderDropped(string path)
    {
        lock (_gate)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(NativeEvent.FolderDropped(path));
            }
        }
    }

    /// <summary>The SPA has saved or discarded the changes: the window closes for good.</summary>
    public void ApproveClose()
    {
        lock (_gate)
        {
            _closeApproved = true;
        }

        bridge.CloseWindow();
    }

    /// <summary>Events for one SPA connection, until it disconnects or the host stops.</summary>
    public async IAsyncEnumerable<NativeEvent> Subscribe([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<NativeEvent>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        try
        {
            while (await WaitAsync(channel.Reader, cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var item))
                {
                    yield return item;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    /// <summary>False once cancelled: a disconnection or the shutdown ends the stream normally, it is no error.</summary>
    private static async ValueTask<bool> WaitAsync(ChannelReader<NativeEvent> reader, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
