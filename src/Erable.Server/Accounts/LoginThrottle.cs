using System.Collections.Concurrent;
using Erable.Server.Hosting;

namespace Erable.Server.Accounts;

/// <summary>
/// Brute-force protection of the login: failures are counted over a sliding window. A login name tried from
/// one address, and one address whatever the login, are blocked beyond their limit (429 whatever the password). A
/// login name alone is never blocked, otherwise anyone knowing it could lock its owner out: once it has failed too
/// often from all addresses together, each attempt only waits a growing, capped delay.
/// In memory: a restart clears it, which is acceptable for a rate limit.
/// </summary>
public sealed class LoginThrottle(ErableHostOptions options, TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, List<DateTimeOffset>> _failures = new(StringComparer.Ordinal);

    /// <summary>Seconds to wait when blocked, or null.</summary>
    public int? RetryAfterSeconds(string login, string address)
    {
        var worst = Math.Max(
            Wait(PairKey(login, address), options.LoginMaxFailures),
            Wait(AddressKey(address), options.LoginMaxFailuresPerAddress));
        return worst > 0 ? worst : null;
    }

    /// <summary>
    /// Delay before answering an attempt on <paramref name="login"/>: zero below <see cref="ErableHostOptions.LoginMaxFailures"/>
    /// failures from all addresses, then <see cref="ErableHostOptions.LoginDelayStep"/> doubled at each failure, up to
    /// <see cref="ErableHostOptions.LoginMaxDelay"/>.
    /// </summary>
    public TimeSpan DelayFor(string login)
    {
        var excess = Count(LoginKey(login)) - options.LoginMaxFailures;
        if (excess < 0)
        {
            return TimeSpan.Zero;
        }

        var factor = Math.Pow(2, Math.Min(excess, 30));
        return TimeSpan.FromTicks((long)Math.Min(options.LoginDelayStep.Ticks * factor, options.LoginMaxDelay.Ticks));
    }

    public void RecordFailure(string login, string address)
    {
        Add(PairKey(login, address));
        Add(AddressKey(address));
        Add(LoginKey(login));
    }

    public void Reset(string login, string address)
    {
        _failures.TryRemove(PairKey(login, address), out _);
        _failures.TryRemove(LoginKey(login), out _);
    }

    private static string PairKey(string login, string address) => $"pair:{address}|{login}";

    private static string AddressKey(string address) => $"address:{address}";

    private static string LoginKey(string login) => $"login:{login}";

    private int Count(string key)
    {
        if (!_failures.TryGetValue(key, out var list))
        {
            return 0;
        }

        lock (list)
        {
            Trim(list);
            return list.Count;
        }
    }

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
