using Erable.Core;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary><see cref="ProjectStore.OpenSnapshot"/>: the read-only view of erable-cli never writes the project.</summary>
public sealed class ProjectSnapshotTests
{
    private readonly FakeTimeProvider _clock = new(Chroniques.Now);

    [Fact]
    public void OpenSnapshot_ClosedProject_WritesNothingInTheFolder()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);
        var before = FolderFingerprint.Of(dir.Path);

        using (var snapshot = ProjectStore.OpenSnapshot(dir.Path).Value)
        {
            snapshot.Repository.GetElement(Chroniques.Ilsa).ShouldNotBeNull().Name.ShouldBe("Ilsa Varenne");
        }

        FolderFingerprint.Of(dir.Path).ShouldBe(before);
    }

    [Fact]
    public async Task OpenSnapshot_ProjectOpenElsewhere_ReadsItsCommittedState()
    {
        using var dir = TempDirectory.Create();
        using var open = Chroniques.Write(dir.Path);
        var service = new ProjectService(open, _clock);
        var revision = open.Repository.GetElement(Chroniques.Orsen)!.Revision;
        (await service.SavePageContentAsync(Chroniques.Orsen, "Nouveau texte.", revision, "camille", cancellationToken: TestContext.Current.CancellationToken))
            .IsSuccess.ShouldBeTrue();

        using var snapshot = ProjectStore.OpenSnapshot(dir.Path).Value;

        snapshot.Repository.GetPageContent(Chroniques.Orsen).ShouldNotBeNull().Markdown.ShouldBe("Nouveau texte.");
    }

    [Fact]
    public void OpenSnapshot_OlderFormat_MigratesTheCopyOnly()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(RepositoryPaths.Combine("spec", "projects", "legacy", "v1"), dir.Path);
        var before = FolderFingerprint.Of(dir.Path);

        using (var snapshot = ProjectStore.OpenSnapshot(dir.Path).Value)
        {
            snapshot.Manifest.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
            snapshot.Database.ReadSchemaVersion().ShouldBe(ProjectFormat.CurrentVersion);
        }

        FolderFingerprint.Of(dir.Path).ShouldBe(before);
    }

    [Fact]
    public void OpenSnapshot_Disposed_DeletesItsCopy()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);

        string copy;
        using (var snapshot = ProjectStore.OpenSnapshot(dir.Path).Value)
        {
            copy = snapshot.Database.Path;
            File.Exists(copy).ShouldBeTrue();
        }

        Directory.Exists(Path.GetDirectoryName(copy)).ShouldBeFalse();
    }

    [Fact]
    public void OpenSnapshot_NotAProject_Fails()
    {
        using var dir = TempDirectory.Create();

        var result = ProjectStore.OpenSnapshot(dir.Path);

        result.IsSuccess.ShouldBeFalse();
        result.Error.Code.ShouldBe(StorageErrors.NotAProject);
    }
}
