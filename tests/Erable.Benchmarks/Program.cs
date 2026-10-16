using System.Globalization;
using Erable.Testing;

namespace Erable.Benchmarks;

/// <summary>
/// Performance tooling (M3-06, docs/testing.md §11). Run in Release:
/// <c>dotnet run -c Release --project tests/Erable.Benchmarks -- &lt;command&gt;</c>.
/// </summary>
internal static class Program
{
    private const string Usage = """
        Commands:
          generate <folder> [--pages 10000] [--categories 300] [--refs 50000] [--attachments-size 2GB] [--attachment-file-size 1MB] [--seed 42] [--light]
              Writes a large project with a fixed seed. --light: 16 MB of attachments in files of 64 KB (CI).
          budgets [--folder <folder>] [--light] [--pages N] [--json <file>]
              Measures the server budgets of docs/testing.md §11.1 over HTTP on a large project (generated in a temporary
              folder unless --folder is given); exits 1 when a p95 is over its budget.
          bench [--pages N] [--update-baseline]
              BenchmarkDotNet on the hot paths (parser, property resolution, save, search); writes artifacts/perf/latest.json,
              appends to artifacts/perf/history.json and compares with spec/perf/baseline.json (exit 1 above +15 %).
          load [--users 50] [--minutes 10 | --seconds N] [--think-ms 2000-6000] [--light] [--folder <folder>] [--json <file>] [--reports <folder>]
              NBomber (docs/testing.md §11.2): a team of virtual users, 80 % reading and 20 % writing, on a server; exits 1 on any
              error, a p95 over its budget or a working set that keeps growing.
          compare <latest.json> <baseline.json> [--threshold 15]
              Compares two result files.
        """;

    private static async Task<int> Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var options = Arguments.Parse(args.Skip(1));
        switch (args[0])
        {
            case "generate":
                return Generate(options);
            case "budgets":
                return await Budgets.RunAsync(options).ConfigureAwait(false);
            case "load":
                return await Load.RunAsync(options).ConfigureAwait(false);
            case "bench":
                return HotPaths.Run(options);
            case "compare":
                return Regression.Compare(options);
            default:
                Console.Error.WriteLine(Usage);
                return 2;
        }
    }

    private static int Generate(Arguments arguments)
    {
        var folder = arguments.Positional.FirstOrDefault();
        if (folder is null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        var generated = LargeProjectFrom(arguments);
        if (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Console.Error.WriteLine($"{folder} is not empty.");
            return 2;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        using (LargeProject.Generate(folder, generated, step => Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] {step}")))
        {
        }

        Console.WriteLine($"[{clock.Elapsed:mm\\:ss}] {generated.Pages} pages, {generated.Categories} categories, {generated.References} references, {Size(generated.AttachmentsBytes)} of attachments in {folder}");
        return 0;
    }

    /// <summary>The generator options named on the command line (<c>--light</c> for the CI size).</summary>
    public static LargeProjectOptions LargeProjectFrom(Arguments arguments)
    {
        var baseline = arguments.Flag("light") ? LargeProjectOptions.Light : new LargeProjectOptions();
        return baseline with
        {
            Pages = arguments.Int("pages") ?? baseline.Pages,
            Categories = arguments.Int("categories") ?? baseline.Categories,
            References = arguments.Int("refs") ?? baseline.References,
            Seed = arguments.Int("seed") ?? baseline.Seed,
            AttachmentsBytes = arguments.Bytes("attachments-size") ?? baseline.AttachmentsBytes,
            AttachmentSize = (int?)arguments.Bytes("attachment-file-size") ?? baseline.AttachmentSize,
        };
    }

    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => (bytes / (double)(1L << 30)).ToString("0.#", CultureInfo.InvariantCulture) + " GB",
        >= 1L << 20 => (bytes / (double)(1L << 20)).ToString("0.#", CultureInfo.InvariantCulture) + " MB",
        _ => bytes.ToString(CultureInfo.InvariantCulture) + " B",
    };
}

/// <summary>Minimal command line: positionals, <c>--name value</c> and <c>--flag</c>.</summary>
internal sealed class Arguments
{
    private readonly Dictionary<string, string?> _named = new(StringComparer.Ordinal);

    public List<string> Positional { get; } = [];

    public static Arguments Parse(IEnumerable<string> args)
    {
        var result = new Arguments();
        var list = args.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (!list[i].StartsWith("--", StringComparison.Ordinal))
            {
                result.Positional.Add(list[i]);
                continue;
            }

            var name = list[i][2..];
            var hasValue = i + 1 < list.Count && !list[i + 1].StartsWith("--", StringComparison.Ordinal);
            result._named[name] = hasValue ? list[++i] : null;
        }

        return result;
    }

    public bool Flag(string name) => _named.ContainsKey(name);

    public string? Value(string name) => _named.GetValueOrDefault(name);

    public int? Int(string name) =>
        Value(name) is { } text ? int.Parse(text, CultureInfo.InvariantCulture) : null;

    /// <summary>A size: <c>2GB</c>, <c>64KB</c>, <c>1048576</c>.</summary>
    public long? Bytes(string name)
    {
        if (Value(name) is not { } text)
        {
            return null;
        }

        var upper = text.Trim().ToUpperInvariant();
        var (digits, factor) = upper switch
        {
            _ when upper.EndsWith("GB", StringComparison.Ordinal) => (upper[..^2], 1L << 30),
            _ when upper.EndsWith("MB", StringComparison.Ordinal) => (upper[..^2], 1L << 20),
            _ when upper.EndsWith("KB", StringComparison.Ordinal) => (upper[..^2], 1L << 10),
            _ => (upper, 1L),
        };
        return (long)(double.Parse(digits, CultureInfo.InvariantCulture) * factor);
    }
}
