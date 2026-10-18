using Erable.Server.Accounts;
using Erable.Server.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Server.Tests;

/// <summary>Brute-force protection without a targeted lockout (audit S2).</summary>
public sealed class LoginThrottleTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);

    private static readonly ErableHostOptions Options = new()
    {
        Mode = HostMode.Server,
        LoginMaxFailures = 3,
        LoginMaxFailuresPerAddress = 40,
        LoginDelayStep = TimeSpan.FromSeconds(1),
        LoginMaxDelay = TimeSpan.FromSeconds(10),
    };

    [Fact]
    public void RetryAfterSeconds_FailuresFromOneAddress_BlocksThatPair()
    {
        var throttle = new LoginThrottle(Options, new FakeTimeProvider(Start));
        for (var i = 0; i < 3; i++)
        {
            throttle.RecordFailure("admin", "203.0.113.7");
        }

        throttle.RetryAfterSeconds("admin", "203.0.113.7").ShouldNotBeNull();
    }

    [Fact]
    public void RetryAfterSeconds_FailuresFromOtherAddresses_NeverBlocksTheOwner()
    {
        var throttle = new LoginThrottle(Options, new FakeTimeProvider(Start));
        for (var i = 0; i < 30; i++)
        {
            throttle.RecordFailure("admin", $"203.0.113.{i}");
        }

        throttle.RetryAfterSeconds("admin", "198.51.100.9").ShouldBeNull();
    }

    [Fact]
    public void DelayFor_BelowTheLimit_IsZero()
    {
        var throttle = new LoginThrottle(Options, new FakeTimeProvider(Start));
        throttle.RecordFailure("admin", "203.0.113.1");
        throttle.RecordFailure("admin", "203.0.113.2");

        throttle.DelayFor("admin").ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void DelayFor_BeyondTheLimit_DoublesAtEachFailureUpToTheCap()
    {
        var throttle = new LoginThrottle(Options, new FakeTimeProvider(Start));
        var delays = new List<TimeSpan>();
        for (var i = 0; i < 9; i++)
        {
            throttle.RecordFailure("admin", $"203.0.113.{i}");
            delays.Add(throttle.DelayFor("admin"));
        }

        delays.Select(d => d.TotalSeconds).ShouldBe([0, 0, 1, 2, 4, 8, 10, 10, 10]);
    }

    [Fact]
    public void DelayFor_AfterTheWindow_IsZeroAgain()
    {
        var clock = new FakeTimeProvider(Start);
        var throttle = new LoginThrottle(Options, clock);
        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure("admin", $"203.0.113.{i}");
        }

        clock.Advance(Options.LoginWindow + TimeSpan.FromSeconds(1));

        throttle.DelayFor("admin").ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Reset_AfterASuccess_ClearsTheDelayOfTheLogin()
    {
        var throttle = new LoginThrottle(Options, new FakeTimeProvider(Start));
        for (var i = 0; i < 5; i++)
        {
            throttle.RecordFailure("admin", $"203.0.113.{i}");
        }

        throttle.Reset("admin", "198.51.100.9");

        throttle.DelayFor("admin").ShouldBe(TimeSpan.Zero);
    }
}
