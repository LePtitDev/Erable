using System.Threading.Channels;
using Erable.Core;
using Erable.Server.Api;
using Erable.Server.Realtime;
using Erable.Storage;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>A client of the project hub, signed in as an account; everything it receives is queued in order.</summary>
public sealed class HubClient : IAsyncDisposable
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    private readonly Channel<object> _events = Channel.CreateUnbounded<object>();
    private readonly Channel<PresenceChangedEvent> _presence = Channel.CreateUnbounded<PresenceChangedEvent>();

    /// <param name="keepAlive">How often the client pings the server; a huge value makes it a client that never answers.</param>
    public HubClient(TestBackend backend, string login, TimeSpan? keepAlive = null)
    {
        ArgumentNullException.ThrowIfNull(backend);
        var token = backend.Accounts.StartSession(login, ServerStore.SessionKindBearer).Token;
        var server = backend.Server;
        var builder = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, ApiRoutes.ProjectHub), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            });
        if (keepAlive is { } interval)
        {
            builder.WithKeepAliveInterval(interval).WithServerTimeout(TimeSpan.FromHours(2));
        }

        Connection = builder.Build();
        Connection.On<ElementChangedEvent>("ElementChanged", e => _events.Writer.TryWrite(e));
        Connection.On<ElementMovedEvent>("ElementMoved", e => _events.Writer.TryWrite(e));
        Connection.On<ElementDeletedEvent>("ElementDeleted", e => _events.Writer.TryWrite(e));
        Connection.On<TreeChangedEvent>("TreeChanged", e => _events.Writer.TryWrite(e));
        Connection.On<PresenceChangedEvent>("PresenceChanged", e => _presence.Writer.TryWrite(e));
    }

    public HubConnection Connection { get; }

    /// <summary>Joins the project; the answer carries the presence at that moment.</summary>
    public async Task<JoinResult> JoinWithPresenceAsync(ElementId projectId)
    {
        if (Connection.State == HubConnectionState.Disconnected)
        {
            await Connection.StartAsync(TestContext.Current.CancellationToken);
        }

        return await Connection.InvokeAsync<JoinResult>("JoinProject", projectId.ToString(), TestContext.Current.CancellationToken);
    }

    public async Task<bool> JoinAsync(ElementId projectId) => (await JoinWithPresenceAsync(projectId)).Joined;

    public Task SetPresenceAsync(ElementId projectId, ElementId? elementId, string mode, string? section = null) =>
        Connection.InvokeAsync("SetPresence", projectId.ToString(), elementId?.ToString(), mode, section, TestContext.Current.CancellationToken);

    /// <summary>The next change event (presence apart, which has its own queue), waiting at most ten seconds.</summary>
    public async Task<T> NextAsync<T>()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Wait);
        var next = await _events.Reader.ReadAsync(timeout.Token);
        return next.ShouldBeOfType<T>();
    }

    /// <summary>The next presence snapshot satisfying <paramref name="condition"/> (the others are skipped), within ten seconds.</summary>
    public async Task<PresenceChangedEvent> PresenceWhereAsync(Func<PresenceChangedEvent, bool> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(Wait);
        while (true)
        {
            var presence = await _presence.Reader.ReadAsync(timeout.Token);
            if (condition(presence))
            {
                return presence;
            }
        }
    }

    /// <summary>Reads the next event with the caller's own cancellation (to check that nothing arrives).</summary>
    public async Task<T> NextAsyncWithToken<T>(CancellationToken cancellation) =>
        (await _events.Reader.ReadAsync(cancellation)).ShouldBeOfType<T>();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
