using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Erable.Benchmarks;

/// <summary>
/// History and regression check of the benchmarks (docs/testing.md §11.2): a result more than 15 % slower than the
/// baseline fails the run; every run is appended to a history file.
/// </summary>
internal static class Regression
{
    public static int Compare(Arguments arguments)
    {
        if (arguments.Positional.Count < 2)
        {
            Console.Error.WriteLine("compare <latest.json> <baseline.json> [--threshold 15]");
            return 2;
        }

        return Compare(arguments.Positional[0], arguments.Positional[1], arguments.Int("threshold") ?? 15);
    }

    /// <summary>Prints each benchmark against its baseline; returns 1 when one is slower by more than <paramref name="thresholdPercent"/>.</summary>
    public static int Compare(string latestPath, string baselinePath, int thresholdPercent)
    {
        var latest = (JsonObject)JsonNode.Parse(File.ReadAllText(latestPath))!;
        var baseline = (JsonObject)JsonNode.Parse(File.ReadAllText(baselinePath))!;
        var regressions = 0;
        Console.WriteLine($"{"Benchmark",-28} {"baseline",14} {"now",14} {"change",9}");
        foreach (var (name, node) in latest)
        {
            var now = node!.GetValue<double>();
            if (baseline[name] is not { } reference)
            {
                Console.WriteLine($"{name,-28} {"(new)",14} {Format(now),14}");
                continue;
            }

            var before = reference.GetValue<double>();
            var change = (now - before) / before * 100;
            var slower = change > thresholdPercent;
            if (slower)
            {
                regressions++;
            }

            Console.WriteLine($"{name,-28} {Format(before),14} {Format(now),14} {change,8:+0.#;-0.#}% {(slower ? "REGRESSION" : string.Empty)}");
        }

        Console.WriteLine(regressions == 0
            ? $"\nNo benchmark is more than {thresholdPercent} % slower than the baseline."
            : $"\n{regressions} benchmark(s) more than {thresholdPercent} % slower than the baseline.");
        return regressions == 0 ? 0 : 1;
    }

    /// <summary>Adds the run to the history file (date and means in nanoseconds).</summary>
    public static void AppendHistory(string path, JsonObject results)
    {
        var history = File.Exists(path) ? (JsonArray)JsonNode.Parse(File.ReadAllText(path))! : [];
        history.Add(new JsonObject
        {
            ["at"] = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            ["machine"] = Environment.MachineName,
            ["means"] = results.DeepClone(),
        });
        File.WriteAllText(path, history.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Format(double nanoseconds) => nanoseconds switch
    {
        >= 1_000_000 => (nanoseconds / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + " ms",
        >= 1_000 => (nanoseconds / 1_000).ToString("0.##", CultureInfo.InvariantCulture) + " µs",
        _ => nanoseconds.ToString("0.#", CultureInfo.InvariantCulture) + " ns",
    };
}
