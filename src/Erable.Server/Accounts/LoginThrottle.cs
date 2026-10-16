using System.Collections.Concurrent;
using Erable.Server.Hosting;

namespace Erable.Server.Accounts;

/// <summary>
/// Brute-force protection of the login (M4-01): failures are counted per login name and per client address over a
/// sliding window; beyond the limit the login answers 429 whatever the password, so that guessing gets nowhere.
/// In memory: a restart clears it, which is acceptable for a rate limit.
/// </summary>
public sealed class LoginThrottle(ErableHostOptions options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failures = new(StringComparer.Ordinal);

    /// <summary>Seconds to wait when blocked, or null.</summary>
    public int? RetryAfterSeconds(string login, string address)
    {
        var worst = Math.Max(
            Wait($"login:{login}", options.LoginMaxFailures),
            Wait($"address:{address}", options.LoginMaxFailuresPerAddress));
        return worst > 0 ? worst : null;
    }

    public void RecordFailure(string login, string address)
    {
        Add($"login:{login}");
        Add($"address:{address}");
    }

    public void Reset(string login) => _failures.TryRemove($"login:{login}", out _);

    private int Wait(string key, int limit)
    {
        if (!_failures.TryGetValue(key, out var list))
        {
            return 0;
        }

        lock (list)
        {
            Trim(list);
            if (list.Count < limit)
            {
                return 0;
            }

            var until = list[^limit] + options.LoginWindow;
            return Math.Max(1, (int)Math.Ceiling((until - clock.GetUtcNow()).TotalSeconds));
        }
    }

    private void Add(string key)
    {
        var list = _failures.GetOrAdd(key, _ => []);
        lock (list)
        {
            Trim(list);
            list.Add(clock.GetUtcNow());
        }
    }

    private void Trim(List<DateTimeOffset> list)
    {
        var cutoff = clock.GetUtcNow() - options.LoginWindow;
        list.RemoveAll(t => t <= cutoff);
    }
}
