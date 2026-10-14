using System.Text.Json.Nodes;
using Erable.Server.Cli;
using Erable.Storage;
using Erable.Testing;
using Erable.Testing.Reference;

namespace Erable.Server.Tests;

public sealed class DoctorCommandTests
{
    private static (int ExitCode, string Output) Run(string folder, bool json = false, bool fix = false)
    {
        using var output = new StringWriter();
        var code = DoctorCommand.Run(folder, json, fix, output);
        return (code, output.ToString());
    }

    private static TempDirectory CopyOfReference()
    {
        var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);
        return dir;
    }

    [Fact]
    public void Run_ReferenceProject_ExitsZero()
    {
        using var dir = CopyOfReference();

        var (code, output) = Run(dir.Path);

        code.ShouldBe(DoctorCommand.Healthy);
        output.ShouldContain("No anomaly.");
    }

    [Fact]
    public void Run_OrphanBlob_ExitsOneThenFixReturnsZero()
    {
        using var dir = CopyOfReference();
        var orphan = Path.Combine(dir.Path, "blobs", "aa", "aa", new string('a', 64));
        Directory.CreateDirectory(Path.GetDirectoryName(orphan)!);
        File.WriteAllBytes(orphan, [1]);

        Run(dir.Path).ExitCode.ShouldBe(DoctorCommand.Anomalies);
        Run(dir.Path, fix: true).ExitCode.ShouldBe(DoctorCommand.Healthy);
        Run(dir.Path).ExitCode.ShouldBe(DoctorCommand.Healthy);
    }

    [Fact]
    public void Run_Json_HasAStableShape()
    {
        using var dir = CopyOfReference();
        File.Delete(Directory.EnumerateFiles(Path.Combine(dir.Path, "blobs"), "*", SearchOption.AllDirectories).First());

        var (code, output) = Run(dir.Path, json: true);

        code.ShouldBe(DoctorCommand.Anomalies);
        var report = JsonNode.Parse(output)!.AsObject();
        report.Select(p => p.Key).ShouldBe(["folder", "formatVersion", "healthy", "findings"]);
        report["healthy"]!.GetValue<bool>().ShouldBeFalse();
        var finding = report["findings"]![0]!.AsObject();
        finding["code"]!.GetValue<string>().ShouldBe(DoctorCodes.BlobMissing);
        finding["severity"]!.GetValue<string>().ShouldBe("error");
        finding["elementId"]!.GetValue<string>().Length.ShouldBe(16);
    }

    [Fact]
    public void Run_NotAProject_ExitsTwo()
    {
        using var dir = TempDirectory.Create();

        Run(dir.Path).ExitCode.ShouldBe(DoctorCommand.Unreadable);
    }

    [Fact]
    public async Task Cli_DoctorCommand_IsWired()
    {
        using var dir = CopyOfReference();

        var code = await ErableCli.RunAsync(["doctor", dir.Path, "--json"]);

        code.ShouldBe(DoctorCommand.Healthy);
    }
}
