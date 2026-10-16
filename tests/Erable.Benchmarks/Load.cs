using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Server.Hosting;
using Erable.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using NBomber.CSharp;

namespace Erable.Benchmarks;

/// <summary>
/// The load test of docs/testing.md §11.2 (M4-09): a team of virtual users (80 % reading, 20 % writing) on a server for
/// several minutes. Fails on any error, on a p95 over the budgets of §11.1, or on a working set that keeps growing.
/// </summary>
internal static class Load
{
    private sealed record Budget(string Step, double P95Ms);

    // The same p95 budgets as the single-user measurements (Budgets.cs): the team must not make the server slower.
    private static readonly Budget[] Budgets =
    [
        new("tree", 150),
        new("open page", 200),
        new("search", 100),
        new("quick search", 100),
        new("save page", 100),
    ];

    private static readonly string[] Lexicon = ["montagne", "village", "caravane", "givre", "sentier", "marchand", "garde", "ordre", "carte", "tempête"];

    private const string Login = "load";
    private const string Password = "load-password-2026";

    public static async Task<int> RunAsync(Arguments arguments)
    {
        var users = arguments.Int("users") ?? 50;
        var duration = TimeSpan.FromSeconds((arguments.Value("seconds") is { } s ? double.Parse(s, CultureInfo.InvariantCulture) : (arguments.Int("minutes") ?? 10) * 60.0));
        var generated = Program.LargeProjectFrom(arguments);
        var work = Directory.CreateTempSubdirectory("erable-load-");
        var folder = arguments.Value("folder") ?? Path.Combine(work.FullName, "project");
        try
        {
            if (arguments.Value("folder") is null)
            {
                Console.WriteLine($"Generating {generated.Pages} pages…");
                using (LargeProject.Generate(folder, generated))
                {
                }
            }

            return await MeasureAsync(folder, work.FullName, users, duration, arguments).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                work.Delete(recursive: true);
            }
            catch (IOException)
            {
                // A leftover temporary folder is not worth failing a run.
            }
        }
    }

    private static async Task<int> MeasureAsync(string folder, string work, int users, TimeSpan duration, Arguments arguments)
    {
        var web = Directory.CreateTempSubdirectory("erable-load-web-");
        await File.WriteAllTextAsync(Path.Combine(web.FullName, "index.html"), "<!doctype html><html></html>").ConfigureAwait(false);
        var data = Directory.CreateTempSubdirectory("erable-load-data-");
        var app = ErableHost.Build(new ErableHostOptions
        {
            Mode = HostMode.Server,
            Port = 0,
            WebRootPath = web.FullName,
            DataDirectory = data.FullName,
            FolderProjects = true,
            BootstrapAdminLogin = Login,
            BootstrapAdminPassword = Password,
        }, builder => builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        await app.StartAsync().ConfigureAwait(false);
        try
        {
            using var http = new HttpClient { BaseAddress = ErableHost.GetListeningUri(app), Timeout = TimeSpan.FromSeconds(30) };
            using var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { login = Login, password = Password, issueToken = true }).ConfigureAwait(false);
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!["token"]!.GetValue<string>();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var opened = await http.PostAsJsonAsync("/api/v1/projects/open", new { folder }).ConfigureAwait(false);
            opened.EnsureSuccessStatusCode();
            var project = (await opened.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!["id"]!.GetValue<string>();
            var root = $"/api/v1/projects/{project}";
            using var treeResponse = await http.GetAsync(new Uri($"{root}/tree", UriKind.Relative)).ConfigureAwait(false);
            var nodes = (await treeResponse.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!["nodes"]!.AsArray();
            var pages = nodes.Where(n => n!["kind"]!.GetValue<string>() == "page").Select(n => n!["id"]!.GetValue<string>()).ToArray();
            Console.WriteLine($"{nodes.Count} elements, {users} users, {duration.TotalSeconds:0} s.");

            // Reads go anywhere; a user writes only the pages of its own slice, so that two users never race on a page
            // (a real conflict is a 409 by design, and it is not what this test measures).
            var scenario = Scenario.Create("team", async context =>
            {
                var random = context.Random;
                var slot = (int)(context.ScenarioInfo.InstanceNumber % users);
                var roll = random.Next(100);
                if (roll < 40)
                {
                    await StepAsync("open page", context, () => ReadAsync(http, $"{root}/pages/{Pick(pages, random)}")).ConfigureAwait(false);
                }
                else if (roll < 55)
                {
                    await StepAsync("tree", context, () => ReadAsync(http, $"{root}/tree")).ConfigureAwait(false);
                }
                else if (roll < 70)
                {
                    await StepAsync("search", context, () => ReadAsync(http, $"{root}/search?q={Lexicon[random.Next(Lexicon.Length)]}")).ConfigureAwait(false);
                }
                else if (roll < 80)
                {
                    await StepAsync("quick search", context, () => ReadAsync(http, $"{root}/search?q={Lexicon[random.Next(Lexicon.Length)][..3]}&nameOnly=true&limit=8")).ConfigureAwait(false);
                }
                else
                {
                    var own = pages.Where((_, index) => index % users == slot).ToArray();
                    await StepAsync("save page", context, () => SaveAsync(http, root, Pick(own.Length > 0 ? own : pages, random), pages, random)).ConfigureAwait(false);
                }

                // Thinking time: a person reads before the next click.
                await Task.Delay(random.Next(300, 1200)).ConfigureAwait(false);
                return Response.Ok();
            })
            .WithoutWarmUp()
            .WithLoadSimulations(Simulation.KeepConstant(copies: users, during: duration));

            var memory = new List<(double Seconds, double MegaBytes)>();
            using var sampler = new CancellationTokenSource();
            var clock = Stopwatch.StartNew();
            var sampling = Task.Run(async () =>
            {
                using var self = Process.GetCurrentProcess();
                while (!sampler.IsCancellationRequested)
                {
                    self.Refresh();
                    memory.Add((clock.Elapsed.TotalSeconds, self.WorkingSet64 / 1048576.0));
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), sampler.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });

            var reports = Path.Combine(arguments.Value("reports") ?? work, "nbomber");
            var stats = NBomberRunner
                .RegisterScenarios(scenario)
                .WithReportFolder(reports)
                .WithReportFileName("load")
                .WithReportFormats(NBomber.Contracts.Stats.ReportFormat.Md, NBomber.Contracts.Stats.ReportFormat.Txt)
                .Run();
            await sampler.CancelAsync().ConfigureAwait(false);
            await sampling.ConfigureAwait(false);

            return Report(stats, memory, users, duration, arguments);
        }
        finally
        {
            await app.DisposeAsync().ConfigureAwait(false);
            try
            {
                web.Delete(recursive: true);
                data.Delete(recursive: true);
            }
            catch (IOException)
            {
                // See above.
            }
        }
    }

    private static int Report(NBomber.Contracts.Stats.NodeStats stats, List<(double Seconds, double MegaBytes)> memory, int users, TimeSpan duration, Arguments arguments)
    {
        var failures = 0;
        var report = new JsonObject { ["users"] = users, ["seconds"] = duration.TotalSeconds };
        var steps = new JsonArray();
        var scenario = stats.ScenarioStats[0];
        Console.WriteLine();
        Console.WriteLine($"{"Step",-14} {"requests",9} {"errors",7} {"p50",8} {"p95",8} {"budget",8}");
        foreach (var step in scenario.StepStats)
        {
            var budget = Budgets.FirstOrDefault(b => b.Step == step.StepName);
            var p95 = step.Ok.Latency.Percent95;
            var over = budget is not null && p95 > budget.P95Ms;
            var errors = step.Fail.Request.Count;
            failures += errors + (over ? 1 : 0);
            Console.WriteLine($"{step.StepName,-14} {step.Ok.Request.Count + errors,9} {errors,7} {step.Ok.Latency.Percent50,6:0.#}ms {p95,6:0.#}ms {budget?.P95Ms ?? 0,6:0}ms {(over ? "OVER BUDGET" : "ok")}");
            steps.Add(new JsonObject
            {
                ["step"] = step.StepName,
                ["requests"] = step.Ok.Request.Count + errors,
                ["errors"] = errors,
                ["p50Ms"] = Math.Round(step.Ok.Latency.Percent50, 2),
                ["p95Ms"] = Math.Round(p95, 2),
                ["p99Ms"] = Math.Round(step.Ok.Latency.Percent99, 2),
                ["budgetMs"] = budget?.P95Ms,
                ["ok"] = errors == 0 && !over,
            });
        }

        report["steps"] = steps;
        report["requestsPerSecond"] = Math.Round(scenario.Ok.Request.RPS, 1);

        var (early, late, growth) = MemoryTrend(memory);
        var leaking = growth > 0.25;
        failures += leaking ? 1 : 0;
        Console.WriteLine($"\nWorking set: {early:0} MB in the second quarter, {late:0} MB in the last ({growth:P0}) {(leaking ? "KEEPS GROWING" : "stable")}.");
        report["memory"] = new JsonObject
        {
            ["secondQuarterMb"] = Math.Round(early, 1),
            ["lastQuarterMb"] = Math.Round(late, 1),
            ["growth"] = Math.Round(growth, 3),
            ["stable"] = !leaking,
            ["samples"] = new JsonArray(memory.Select(m => (JsonNode)new JsonObject { ["s"] = Math.Round(m.Seconds), ["mb"] = Math.Round(m.MegaBytes, 1) }).ToArray()),
        };

        if (arguments.Value("json") is { } output)
        {
            File.WriteAllText(output, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        Console.WriteLine(failures == 0 ? "\nLoad test passed." : $"\nLoad test failed ({failures}).");
        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    /// Mean working set of the second and of the last quarter of the run (the first one is the warm-up: JIT, caches). A
    /// server that does not leak settles; more than 25 % of growth between the two is reported as a leak.
    /// </summary>
    private static (double Early, double Late, double Growth) MemoryTrend(List<(double Seconds, double MegaBytes)> samples)
    {
        if (samples.Count < 8)
        {
            return (0, 0, 0);
        }

        var quarter = samples.Count / 4;
        var early = samples.Skip(quarter).Take(quarter).Average(s => s.MegaBytes);
        var late = samples.Skip(samples.Count - quarter).Average(s => s.MegaBytes);
        return (early, late, early <= 0 ? 0 : (late - early) / early);
    }

    private static string Pick(string[] list, Random random) => list[random.Next(list.Length)];

    /// <summary>One measured request: NBomber records its latency and counts a failure for a non-2xx answer.</summary>
    private static async Task StepAsync(string name, NBomber.Contracts.IScenarioContext context, Func<Task<bool>> operation)
    {
        await NBomber.CSharp.Step.Run(name, context, async () =>
        {
            try
            {
                return await operation().ConfigureAwait(false) ? Response.Ok() : Response.Fail();
            }
            catch (HttpRequestException)
            {
                return Response.Fail();
            }
            catch (TaskCanceledException)
            {
                return Response.Fail();
            }
        }).ConfigureAwait(false);
    }

    private static async Task<bool> ReadAsync(HttpClient http, string path)
    {
        using var response = await http.GetAsync(new Uri(path, UriKind.Relative)).ConfigureAwait(false);
        _ = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    /// <summary>A save as the editor does it: the revision read, a few lines more, two references.</summary>
    private static async Task<bool> SaveAsync(HttpClient http, string root, string page, string[] pages, Random random)
    {
        using var current = await http.GetAsync(new Uri($"{root}/pages/{page}/content", UriKind.Relative)).ConfigureAwait(false);
        if (!current.IsSuccessStatusCode)
        {
            return false;
        }

        var content = (await current.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!;
        var markdown = content["markdown"]!.GetValue<string>() + $"\n\nUne ligne de plus, vers [[{Pick(pages, random)}]] et [[{Pick(pages, random)}]].";
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"{root}/pages/{page}/content", UriKind.Relative))
        {
            Content = JsonContent.Create(new { markdown }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{content["revision"]!.GetValue<long>().ToString(CultureInfo.InvariantCulture)}\"");
        using var response = await http.SendAsync(request).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }
}
