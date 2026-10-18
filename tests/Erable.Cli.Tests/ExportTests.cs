using System.Text.Json.Nodes;
using Erable.Testing;
using Erable.Testing.Reference;

namespace Erable.Cli.Tests;

/// <summary><c>erable-cli export</c> and the Markdown it writes (docs/cli.md §Export).</summary>
public sealed class ExportTests : IDisposable
{
    private readonly CliProject _project = new();

    public void Dispose() => _project.Dispose();

    [Fact]
    public async Task Export_ReferenceProject_WritesTheDocumentedLayout()
    {
        var output = _project.Scratch("out");

        var run = await CliProject.RunAsync("export", _project.Folder, "--output", output, "--markdown");

        run.Code.ShouldBe(CliApp.Success);
        run.Json["pages"]!.GetValue<int>().ShouldBe(9);
        run.Json["missingAttachments"]!.AsArray().ShouldBeEmpty();
        foreach (var file in new[] { "project.json", "tree.json", "schema.json", "links.json", "attachments.json" })
        {
            JsonNode.Parse(File.ReadAllText(Path.Combine(output, file)))!["contract"]!.GetValue<string>().ShouldBe(Contract.Version);
        }

        File.Exists(Path.Combine(output, "pages", $"{Chroniques.Ilsa}.json")).ShouldBeTrue();
        File.Exists(Path.Combine(output, "pages", $"{Chroniques.Ilsa}.md")).ShouldBeTrue();
        File.Exists(Path.Combine(output, "attachments", "91F88043221F6AF8", "portrait_ilsa.png")).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_Markdown_TurnsReferencesIntoRelativeLinks()
    {
        var output = _project.Scratch("out");

        await CliProject.RunAsync("export", _project.Folder, "--output", output, "--markdown");

        var markdown = File.ReadAllText(Path.Combine(output, "pages", $"{Chroniques.Ilsa}.md"));
        markdown.ShouldStartWith("---\nid: \"3F2A9C14E07B5D21\"\ntitle: \"Ilsa Varenne\"\n");
        markdown.ShouldContain("  \"niveau\": 72\n");
        markdown.ShouldContain($"[Hautbois](<{Chroniques.Hautbois}.md>)");
        markdown.ShouldContain("![Portrait par Malo L.](<../attachments/91F88043221F6AF8/portrait_ilsa.png>)");
        markdown.ShouldNotContain("[[");
    }

    [Fact]
    public async Task Export_NoAttachments_WritesNoFile()
    {
        var output = _project.Scratch("out");

        var run = await CliProject.RunAsync("export", _project.Folder, "--output", output, "--no-attachments");

        run.Json["attachments"]!.GetValue<int>().ShouldBe(0);
        Directory.Exists(Path.Combine(output, "attachments")).ShouldBeFalse();
        File.Exists(Path.Combine(output, "attachments.json")).ShouldBeTrue();
    }

    [Fact]
    public async Task Export_NonEmptyFolder_RefusesWithoutForce()
    {
        var output = Directory.CreateDirectory(_project.Scratch("out")).FullName;
        await File.WriteAllTextAsync(Path.Combine(output, "notes.txt"), "mine", TestContext.Current.CancellationToken);

        var run = await CliProject.RunAsync("export", _project.Folder, "--output", output);

        run.Code.ShouldBe(CliApp.OutputError);
        run.ErrorJson["error"]!["code"]!.GetValue<string>().ShouldBe(CliErrors.OutputNotEmpty);
        Directory.EnumerateFileSystemEntries(output).Count().ShouldBe(1);
    }

    [Fact]
    public async Task Export_Force_ReplacesThePreviousExportOnly()
    {
        var output = _project.Scratch("out");
        await CliProject.RunAsync("export", _project.Folder, "--output", output, "--markdown");
        await File.WriteAllTextAsync(Path.Combine(output, "notes.txt"), "mine", TestContext.Current.CancellationToken);

        var run = await CliProject.RunAsync("export", _project.Folder, "--output", output, "--force");

        run.Code.ShouldBe(CliApp.Success);
        File.ReadAllText(Path.Combine(output, "notes.txt")).ShouldBe("mine");
        File.Exists(Path.Combine(output, "pages", $"{Chroniques.Ilsa}.md")).ShouldBeFalse();
    }

    [Fact]
    public async Task Export_Twice_GivesIdenticalFilesButTheProjectFolder()
    {
        var first = _project.Scratch("first");
        var second = _project.Scratch("second");

        await CliProject.RunAsync("export", _project.Folder, "--output", first, "--markdown");
        await CliProject.RunAsync("export", _project.Folder, "--output", second, "--markdown");

        FolderFingerprint.Of(first).ShouldBe(FolderFingerprint.Of(second));
    }

    [Fact]
    public void Rewrite_ReferenceInCode_StaysAsWritten()
    {
        using var dir = TempDirectory.Create();
        var builder = Project.Build()
            .WithPage("Source", markdown: "Voir `[[@Cible]]` puis [[@Cible#suite|la suite]] et [[0123456789ABCDEF]].")
            .WithPage("Cible");
        using (ProjectSeeder.Create(builder, dir.Path))
        {
        }

        using var content = ProjectContent.Open(dir.Path).Value;
        var target = builder.IdOf("Cible");

        MarkdownExport.Rewrite(content, $"Voir `[[{target}]]` puis [[{target}#suite|la suite]] et [[0123456789ABCDEF]].")
            .ShouldBe($"Voir `[[{target}]]` puis [la suite](<{target}.md#suite>) et [[0123456789ABCDEF]].");
    }

    [Theory]
    [InlineData("carte.png", "carte.png")]
    [InlineData("a:b*c?.txt", "a_b_c_.txt")]
    [InlineData("rapport. ", "rapport")]
    [InlineData("CON.txt", "_CON.txt")]
    [InlineData("..", "file")]
    public void SafeFileName_Of_GivesANameValidEverywhere(string name, string expected)
    {
        SafeFileName.Of(name).ShouldBe(expected);
    }
}
