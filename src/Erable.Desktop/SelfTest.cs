using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Erable.Server.Api;

namespace Erable.Desktop;

/// <summary>
/// <c>--self-test</c> (docs/testing.md §10): smoke test of the packaged application on macOS and Linux (and Windows).
/// The shell checks its own loopback protection (E2E-16-02), the SPA runs a scripted journey (E2E-16-01) and reports
/// back; the process exits with 0 or 1. It grants nothing beyond a normal start: same token, same endpoints.
/// </summary>
internal static class SelfTest
{
    public const string Flag = "--self-test";
    public const string ReportFlag = "--self-test-report";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>Overrides <see cref="StartupBudget"/>, in milliseconds: the CI runners set 10000 (docs/testing.md §10).</summary>
    public const string StartupBudgetVariable = "ERABLE_STARTUP_BUDGET_MS";

    /// <summary>
    /// E2E-16-01 (M0): the home screen is displayed less than 3 s after launch. Shared CI runners (software rendering,
    /// cold disks, Xvfb) may raise it through <see cref="StartupBudgetVariable"/>; the measure is reported either way.
    /// </summary>
    public static readonly TimeSpan StartupBudget =
        int.TryParse(Environment.GetEnvironmentVariable(StartupBudgetVariable), NumberStyles.None, CultureInfo.InvariantCulture, out var ms) && ms > 0
            ? TimeSpan.FromMilliseconds(ms)
            : TimeSpan.FromSeconds(3);

    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

    private static readonly JsonSerializerOptions ReportJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public sealed record Step(string Name, bool Ok, double DurationMs, string? Detail);

    public sealed record Result(bool Success, IReadOnlyList<Step> Steps);

    /// <summary>Shell-side checks, run while the window loads.</summary>
    public static async Task<IReadOnlyList<Step>> CheckLoopbackProtectionAsync(Uri baseAddress)
    {
        var steps = new List<Step>();
        using var http = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(5) };

        await RunAsync(steps, "loopback-only", async () =>
        {
            if (baseAddress.Host != "127.0.0.1")
            {
                return (false, $"listening on {baseAddress.Host}");
            }

            var others = (await Dns.GetHostAddressesAsync(Dns.GetHostName()).ConfigureAwait(false))
                .Where(a => !IPAddress.IsLoopback(a) && a.AddressFamily == AddressFamily.InterNetwork)
                .ToList();
            foreach (var address in others)
            {
                using var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                using var timeout = new CancellationTokenSource(ConnectTimeout);
                try
                {
                    await socket.ConnectAsync(address, baseAddress.Port, timeout.Token).ConfigureAwait(false);
                    return (false, $"reachable through {address}");
                }
                catch (Exception ex) when (ex is SocketException or OperationCanceledException)
                {
                    // Expected: refused (or dropped by a firewall) — nothing listens there.
                }
            }

            return (true, $"127.0.0.1:{baseAddress.Port}, refused on {others.Count} other interface(s)");
        }).ConfigureAwait(false);

        await RunAsync(steps, "token-required", async () =>
        {
            using var response = await http.GetAsync(new Uri("/api/info", UriKind.Relative)).ConfigureAwait(false);
            return (response.StatusCode == HttpStatusCode.Unauthorized, $"GET /api/info without token → {(int)response.StatusCode}");
        }).ConfigureAwait(false);

        await RunAsync(steps, "host-checked", async () =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/");
            request.Headers.Host = $"evil.example:{baseAddress.Port}";
            using var response = await http.SendAsync(request).ConfigureAwait(false);
            return (response.StatusCode == HttpStatusCode.BadRequest, $"foreign Host → {(int)response.StatusCode}");
        }).ConfigureAwait(false);

        return steps;
    }

    /// <summary>Combines shell checks, the SPA report and the startup budget.</summary>
    public static Result Combine(IReadOnlyList<Step> shellSteps, SelfTestReport? report, TimeSpan sinceProcessStart)
    {
        var steps = new List<Step>(shellSteps);
        if (report is null)
        {
            steps.Add(new Step("spa-report", false, Timeout.TotalMilliseconds, "no report from the SPA before the timeout"));
        }
        else
        {
            steps.AddRange(report.Steps.Select(s => new Step(s.Name, s.Ok, s.DurationMs, s.Detail)));
            // The report arrives after the whole journey: the home screen was shown that much earlier.
            var afterHome = TimeSpan.FromMilliseconds(Math.Max(0, (report.ReportedAtMs ?? report.ReadyAtMs) - report.ReadyAtMs));
            var homeShown = sinceProcessStart - afterHome;
            steps.Add(new Step(
                "startup-budget",
                homeShown <= StartupBudget,
                homeShown.TotalMilliseconds,
                $"home screen {homeShown.TotalMilliseconds:F0} ms after launch (budget {StartupBudget.TotalMilliseconds:F0} ms)"));
        }

        return new Result(steps.All(s => s.Ok), steps);
    }

    public static void Write(Result result, string? reportPath)
    {
        var json = JsonSerializer.Serialize(result, ReportJson);
        Console.Out.WriteLine(json);
        if (reportPath is not null)
        {
            File.WriteAllText(reportPath, json);
        }
    }

    public static TimeSpan SinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }

    private static async Task RunAsync(List<Step> steps, string name, Func<Task<(bool Ok, string Detail)>> check)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var (ok, detail) = await check().ConfigureAwait(false);
            steps.Add(new Step(name, ok, stopwatch.Elapsed.TotalMilliseconds, detail));
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TaskCanceledException)
        {
            steps.Add(new Step(name, false, stopwatch.Elapsed.TotalMilliseconds, ex.Message));
        }
    }
}
