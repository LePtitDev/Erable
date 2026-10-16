using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Search;
using Erable.Storage;
using Erable.Testing;

namespace Erable.Benchmarks;

/// <summary>
/// BenchmarkDotNet on the hot paths of Core and Storage (docs/testing.md §11.2): the Markdown parser, the resolution of
/// the properties of a page, saving a page, searching. The project is generated once (<c>ERABLE_BENCH_PAGES</c> pages,
/// 3 000 by default) in a temporary folder.
/// </summary>
[MemoryDiagnoser]
public class HotPathBenchmarks
{
    private string _folder = string.Empty;
    private OpenedProject _project = null!;
    private ProjectService _service = null!;
    private string _longPage = string.Empty;
    private string _hugeParagraph = string.Empty;
    private ElementId[] _pages = [];
    private ElementId _niveau;
    private int _next;

    [GlobalSetup]
    public void Setup()
    {
        var pages = int.TryParse(Environment.GetEnvironmentVariable("ERABLE_BENCH_PAGES"), out var n) ? n : 3000;
        _folder = Path.Combine(Directory.CreateTempSubdirectory("erable-bench-").FullName, "large");
        _project = LargeProject.Generate(_folder, new LargeProjectOptions(Pages: pages, Categories: 300, References: pages * 5, AttachmentsBytes: 0));
        _service = new ProjectService(_project, TimeProvider.System);
        _pages = [.. _project.Repository.LoadTree().Nodes.Where(node => node.Kind == ElementKind.Page).Select(node => node.Id)];
        _niveau = _project.Repository.LoadTree().Nodes.First(node => node.Name == "Niveau").Id;
        string Line(int i) => $"Ligne {i} avec [[{_pages[i % _pages.Length]}]] et un peu de **texte** et un [lien](https://exemple.org).";

        // 5 000 lines in paragraphs of five lines, as a long page is written.
        _longPage = string.Join("\n\n", Enumerable.Range(0, 1000).Select(p => string.Join('\n', Enumerable.Range(p * 5, 5).Select(Line))));
        _hugeParagraph = string.Join('\n', Enumerable.Range(0, 1000).Select(Line));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _project.Dispose();
        try
        {
            Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);
        }
        catch (IOException)
        {
            // Left in the temporary folder.
        }
    }

    /// <summary>The analysis of a page of 5 000 lines and 5 000 references (budget: typing and saving stay instant).</summary>
    [Benchmark]
    public MarkdownAnalysis ParseLongPage() => ErableMarkdown.Analyze(_longPage);

    /// <summary>
    /// 1 000 lines with no blank line, i.e. one paragraph: Markdig's inline processing grows faster than the size here
    /// (about 2 s for 5 000 lines). Tracked so that a fix, or a worsening, shows.
    /// </summary>
    [Benchmark]
    public MarkdownAnalysis ParseOneHugeParagraph() => ErableMarkdown.Analyze(_hugeParagraph);

    /// <summary>Effective properties of one page (inheritance, defaults, rules), the loading of the state included.</summary>
    [Benchmark]
    public int ResolvePageProperties()
    {
        using var connection = _project.Database.OpenConnection();
        var page = _pages[_next++ % _pages.Length];
        return StructureState.Load(connection, [page]).ResolvePage(page).Count;
    }

    /// <summary>One save: content, plain text, references, derived values, FTS (by trigger), revision.</summary>
    [Benchmark]
    public long SavePage()
    {
        var page = _pages[_next++ % _pages.Length];
        var content = _project.Repository.GetPageContent(page)!;
        return _service.SavePageContentAsync(page, content.Markdown + "\nUne ligne.", content.Revision, "bench").GetAwaiter().GetResult().Value.Revision;
    }

    /// <summary>The explorer tree of the project: read from the database and rebuilt in memory (budget: 150 ms with the response).</summary>
    [Benchmark]
    public int LoadTree() => _service.LoadTree().Nodes.Count();

    /// <summary>What the tree endpoint adds to the loading: ordering, duplicate names, counters.</summary>
    [Benchmark]
    public int TreeProjection()
    {
        var tree = _project.Repository.LoadTree();
        var duplicates = tree.DuplicateNames();
        return tree.Nodes.Where(n => n.Kind != ElementKind.Attachment).OrderBy(n => n.SortOrder).ThenBy(n => n.Id.Value)
            .Count(n => duplicates.Contains(n.Id));
    }

    [Benchmark]
    public int SearchText() => Search("montagne village");

    [Benchmark]
    public int SearchTextWithFilters() => Search("givre", new SearchFilter(
        new HashSet<ElementKind> { ElementKind.Page }, [], false,
        [_service.CreateCondition(_niveau, ConditionOp.Greater, "50").Value], null, false));

    [Benchmark]
    public int SearchOutOfRules() => Search(null, new SearchFilter(new HashSet<ElementKind>(), [], false, [], null, true), SearchSort.Name);

    private int Search(string? text, SearchFilter? filter = null, SearchSort sort = SearchSort.Relevance) =>
        _service.Search(new SearchRequest(text, false, filter ?? SearchFilter.None, sort, 0, 25)).Hits.Count;
}

internal static class HotPaths
{
    public static int Run(Arguments arguments)
    {
        if (arguments.Int("pages") is { } pages)
        {
            Environment.SetEnvironmentVariable("ERABLE_BENCH_PAGES", pages.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        // In-process: no generated project to build for each benchmark, so the run stays short and works anywhere.
        var config = ManualConfig.CreateEmpty()
            .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
            .AddLogger(BenchmarkDotNet.Loggers.ConsoleLogger.Default)
            .AddColumnProvider(BenchmarkDotNet.Columns.DefaultColumnProviders.Instance)
            .WithOptions(ConfigOptions.DisableOptimizationsValidator)
            .WithArtifactsPath(Path.Combine(RepositoryPaths.Combine("artifacts"), "perf", "benchmarkdotnet"));
        var summary = BenchmarkRunner.Run<HotPathBenchmarks>(config);

        var results = new JsonObject();
        foreach (var report in summary.Reports.Where(r => r.ResultStatistics is not null))
        {
            results[report.BenchmarkCase.Descriptor.WorkloadMethod.Name] = Math.Round(report.ResultStatistics!.Mean, 1);
        }

        var directory = Path.Combine(RepositoryPaths.Combine("artifacts"), "perf");
        Directory.CreateDirectory(directory);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var latest = Path.Combine(directory, "latest.json");
        File.WriteAllText(latest, results.ToJsonString(options), new UTF8Encoding(false));
        Regression.AppendHistory(Path.Combine(directory, "history.json"), results);
        Console.WriteLine($"Means (ns) written to {latest}.");

        var baseline = RepositoryPaths.Combine("spec", "perf", "baseline.json");
        if (arguments.Flag("update-baseline"))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(baseline)!);
            File.WriteAllText(baseline, results.ToJsonString(options), new UTF8Encoding(false));
            Console.WriteLine($"Baseline updated: {baseline}.");
            return 0;
        }

        return File.Exists(baseline) ? Regression.Compare(latest, baseline, 15) : 0;
    }
}
