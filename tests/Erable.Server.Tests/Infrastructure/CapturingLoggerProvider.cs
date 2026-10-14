using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Erable.Server.Tests.Infrastructure;

/// <summary>Records every log message (all levels), to assert that secrets never reach the logs.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _messages);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            messages.Enqueue($"{category} scope: {state}");
            return null;
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue($"{category} {logLevel}: {formatter(state, exception)} {state} {exception}");
        }
    }
}
