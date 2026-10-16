using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Server.Hosting;
using Erable.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace Erable.Benchmarks;

/// <summary>
/// The server side of the budgets of docs/testing.md §11.1, measured over real HTTP (Kestrel on loopback) on a large
/// project: tree, page, save, search. The browser side (tree displayed, palette, typing) is measured by the Playwright
/// <c>@perf</c> tests. A p95 over its budget makes the command fail.
/// </summary>
internal static class Budgets
{
    private sealed record Budget(string Name, double P95Ms, Func<Context, Task<List<double>>> Measure);

    private sealed record Context(HttpClient Http, string Base, string[] Pages, Random Random, Dictionary<string, string> Properties, string[] Categories);

    public static async Task<int> RunAsync(Arguments arguments)
    {
        var generated = Program.LargeProjectFrom(arguments);
        var temp = arguments.Value("folder") is null ? Directory.CreateTempSubdirectory("erable-perf-") : null;
        var folder = arguments.Value("folder") ?? Path.Combine(temp!.FullName, "large");
        try
        {
            if (arguments.Value("folder") is null)
            {
                Console.WriteLine($"Generating {generated.Pages} pages in {folder}…");
                var clock = Stopwatch.StartNew();
                using (LargeProject.Generate(folder, generated))
                {
                }

                Console.WriteLine($"Generated in {clock.Elapsed.TotalSeconds:0.#} s.");
            }

            return await MeasureAsync(folder, arguments).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                temp?.Delete(recursive: true);
            }
            catch (IOException)
            {
                // A leftover temporary folder is not worth failing a measurement.
            }
        }
    }

    private static async Task<int> MeasureAsync(string folder, Arguments arguments)
    {
        var web = Directory.CreateTempSubdirectory("erable-perf-web-");
        await File.WriteAllTextAsync(Path.Combine(web.FullName, "index.html"), "<!doctype html><html></html>").ConfigureAwait(false);
        var data = Directory.CreateTempSubdirectory("erable-perf-data-");
        var app = ErableHost.Build(new ErableHostOptions
        {
            Mode = HostMode.Server,
            Port = 0,
            WebRootPath = web.FullName,
            DataDirectory = data.FullName,
            FolderProjects = true,
            BootstrapAdminLogin = "bench",
            BootstrapAdminPassword = "bench-password-2026",
        }, builder => builder.Logging.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        await app.StartAsync().ConfigureAwait(false);
        try
        {
            using var http = new HttpClient { BaseAddress = ErableHost.GetListeningUri(app), Timeout = TimeSpan.FromMinutes(2) };
            using var login = await http.PostAsJsonAsync("/api/v1/auth/login", new { login = "bench", password = "bench-password-2026", issueToken = true }).ConfigureAwait(false);
            login.EnsureSuccessStatusCode();
            var token = (await login.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!["token"]!.GetValue<string>();
            http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            var failures = 0;

            var opening = Stopwatch.StartNew();
            using var opened = await http.PostAsJsonAsync("/api/v1/projects/open", new { folder }).ConfigureAwait(false);
            opened.EnsureSuccessStatusCode();
            var project = (await opened.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!["id"]!.GetValue<string>();
            var root = $"/api/v1/projects/{project}";
            using var firstTree = await http.GetAsync(new Uri($"{root}/tree", UriKind.Relative)).ConfigureAwait(false);
            var tree = (await firstTree.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!;
            var openingMs = opening.Elapsed.TotalMilliseconds;
            var nodes = tree["nodes"]!.AsArray();
            Console.WriteLine($"{nodes.Count} elements in the tree.");

            var pages = nodes.Where(n => n!["kind"]!.GetValue<string>() == "page").Select(n => n!["id"]!.GetValue<string>()).ToArray();
            var categories = nodes.Where(n => n!["kind"]!.GetValue<string>() == "category").Select(n => n!["id"]!.GetValue<string>()).ToArray();
            using var propertiesResponse = await http.GetAsync(new Uri($"{root}/properties", UriKind.Relative)).ConfigureAwait(false);
            var properties = (await propertiesResponse.Content.ReadFromJsonAsync<JsonArray>().ConfigureAwait(false))!
                .ToDictionary(p => p!["key"]!.GetValue<string>(), p => p!["id"]!.GetValue<string>());
            var context = new Context(http, root, pages, new Random(7), properties, categories);

            var budgets = new List<Budget>
            {
                new("Open the project and load the tree (API side, cold)", 1000, _ => Task.FromResult(new List<double> { openingMs })),
                new("Tree (API)", 150, c => Repeat(30, () => Get(c, "/tree"))),
                new("Open a page (API)", 200, c => Repeat(60, () => Get(c, $"/pages/{Pick(c)}"))),
                new("Save a page (index, refs, FTS, validation)", 100, c => Repeat(40, () => Save(c))),
                new("Full-text search", 100, c => Repeat(60, () => Search(c, "q=" + Word(c)))),
                new("Full-text search + type and category filters", 100, c => Repeat(60, () => Search(c, $"q={Word(c)}&kind=page&category={Pick(c.Categories, c)}&subCategories=true"))),
                new("Search with a condition on an inherited property", 100, c => Repeat(60, () => Search(c, $"where={c.Properties["niveau"]}:gt:{c.Random.Next(1, 90)}&sort=name"))),
                new("Search « hors règles » + date", 100, c => Repeat(30, () => Search(c, "outOfRules=true&modified=month&sort=modified"))),
                new("Quick search (names)", 100, c => Repeat(60, () => Search(c, $"q={Word(c)[..3]}&nameOnly=true&limit=8"))),
            };

            var report = new JsonArray();
            Console.WriteLine();
            Console.WriteLine($"{"Operation",-60} {"p50",8} {"p95",8} {"max",8} {"budget",8}");
            foreach (var budget in budgets)
            {
                var samples = await budget.Measure(context).ConfigureAwait(false);
                samples.Sort();
                var p50 = Percentile(samples, 50);
                var p95 = Percentile(samples, 95);
                var ok = p95 <= budget.P95Ms;
                if (!ok)
                {
                    failures++;
                }

                Console.WriteLine($"{budget.Name,-60} {p50,6:0.#}ms {p95,6:0.#}ms {samples[^1],6:0.#}ms {budget.P95Ms,6:0}ms {(ok ? "ok" : "OVER BUDGET")}");
                report.Add(new JsonObject
                {
                    ["name"] = budget.Name,
                    ["p50Ms"] = Math.Round(p50, 2),
                    ["p95Ms"] = Math.Round(p95, 2),
                    ["budgetMs"] = budget.P95Ms,
                    ["samples"] = samples.Count,
                    ["ok"] = ok,
                });
            }

            if (arguments.Value("json") is { } output)
            {
                await File.WriteAllTextAsync(output, report.ToJsonString(new JsonSerializerOptions { WriteIndented = true })).ConfigureAwait(false);
            }

            Console.WriteLine(failures == 0 ? "\nAll server budgets are met." : $"\n{failures} budget(s) exceeded.");
            return failures == 0 ? 0 : 1;
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

    private static async Task<List<double>> Repeat(int times, Func<Task<double>> operation)
    {
        // Two warm-up calls (JIT, SQLite page cache) are not counted: budgets are for a running application.
        await operation().ConfigureAwait(false);
        await operation().ConfigureAwait(false);
        var samples = new List<double>(times);
        for (var i = 0; i < times; i++)
        {
            samples.Add(await operation().ConfigureAwait(false));
        }

        return samples;
    }

    private static string Pick(Context c) => Pick(c.Pages, c);

    private static string Pick(string[] list, Context c) => list[c.Random.Next(list.Length)];

    private static readonly string[] Lexicon = ["montagne", "village", "caravane", "givre", "sentier", "marchand", "garde", "ordre", "carte", "tempête"];

    private static string Word(Context c) => Lexicon[c.Random.Next(Lexicon.Length)];

    private static async Task<double> Get(Context c, string path)
    {
        var clock = Stopwatch.StartNew();
        using var response = await c.Http.GetAsync(new Uri(c.Base + path, UriKind.Relative)).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        _ = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return clock.Elapsed.TotalMilliseconds;
    }

    private static Task<double> Search(Context c, string query) => Get(c, "/search?" + query);

    /// <summary>A save as the editor does it: the current revision in <c>If-Match</c>, a few lines more, two references.</summary>
    private static async Task<double> Save(Context c)
    {
        var page = Pick(c);
        using var current = await c.Http.GetAsync(new Uri($"{c.Base}/pages/{page}/content", UriKind.Relative)).ConfigureAwait(false);
        var content = (await current.Content.ReadFromJsonAsync<JsonObject>().ConfigureAwait(false))!;
        var markdown = content["markdown"]!.GetValue<string>() + $"\n\nUne ligne de plus, vers [[{Pick(c)}]] et [[{Pick(c)}]].";
        using var request = new HttpRequestMessage(HttpMethod.Put, new Uri($"{c.Base}/pages/{page}/content", UriKind.Relative))
        {
            Content = JsonContent.Create(new { markdown }),
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{content["revision"]!.GetValue<long>().ToString(CultureInfo.InvariantCulture)}\"");
        var clock = Stopwatch.StartNew();
        using var response = await c.Http.SendAsync(request).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return clock.Elapsed.TotalMilliseconds;
    }

    private static double Percentile(List<double> sorted, double percentile)
    {
        var rank = Math.Ceiling(percentile / 100 * sorted.Count) - 1;
        return sorted[(int)Math.Clamp(rank, 0, sorted.Count - 1)];
    }
}
