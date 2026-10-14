#if ERABLE_TEST_ENDPOINTS
using System.Diagnostics;

namespace Erable.Server.Testing;

/// <summary>
/// Backend clock of test mode: starts at a chosen instant and keeps ticking from there, so that dates shown by the
/// SPA (« il y a 2 h ») are stable while successive saves stay ordered.
/// </summary>
public sealed class TestClock : TimeProvider
{
    /// <summary>13 October 2026, 10:00 Europe/Paris (docs/features.md §1.4).</summary>
    public static readonly DateTimeOffset ReferenceDate = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private readonly Lock _gate = new();
    private DateTimeOffset _origin;
    private long _originTimestamp;

    public TestClock(DateTimeOffset start)
    {
        Set(start);
    }

    public void Set(DateTimeOffset now)
    {
        lock (_gate)
        {
            _origin = now.ToUniversalTime();
            _originTimestamp = Stopwatch.GetTimestamp();
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _origin + Stopwatch.GetElapsedTime(_originTimestamp);
        }
    }
}
#endif
