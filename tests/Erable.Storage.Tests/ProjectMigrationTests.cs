using System.Text.Json.Nodes;
using Dapper;
using Erable.Core;
using Erable.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>
/// Opening a project of an older format (F01.R3): never migrated without confirmation, always after a backup,
/// and back to its original state when anything fails.
/// </summary>
public sealed class ProjectMigrationTests : IDisposable
{
    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));

    private static readonly ElementId Ilsa = ElementId.Parse("3F2A9C14E07B5D21");

    public static TheoryData<int> OlderVersions =>
        [.. Enumerable.Range(1, ProjectFormat.CurrentVersion - 1)];

    public void Dispose() => _dir.Dispose();

    private string Legacy(int version)
    {
        FolderFingerprint.Copy(RepositoryPaths.Combine("spec", "projects", "legacy", $"v{version}"), _dir.Path);
        return _dir.Path;
    }

    private int ManifestVersion() =>
        JsonNode.Parse(File.ReadAllText(_dir.Combine("erable.json")))!["formatVersion"]!.GetValue<int>();

    private string[] Backups() =>
        [.. Directory.GetFiles(_dir.Path, ProjectFiles.BackupPrefix + "*").Select(Path.GetFileName).OfType<string>()];

    [Theory]
    [MemberData(nameof(OlderVersions))]
    public void Open_OlderFormatWithoutConfirmation_AsksAndTouchesNothing(int version)
    {
        var folder = Legacy(version);
        var before = FolderFingerprint.Of(folder);

        var result = ProjectStore.Open(folder, _clock, migrate: false);

        result.Error.Code.ShouldBe(StorageErrors.MigrationRequired);
        result.Error.Extensions!["fromVersion"].ShouldBe(version);
        result.Error.Extensions["toVersion"].ShouldBe(ProjectFormat.CurrentVersion);
        FolderFingerprint.Of(folder).ShouldBe(before);
        File.Exists(_dir.Combine(ProjectFiles.Lock)).ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(OlderVersions))]
    public void Open_OlderFormatConfirmed_BacksUpThenMigrates(int version)
    {
        // E2E-01-05
        var folder = Legacy(version);

        using (var project = ProjectStore.Open(folder, _clock, migrate: true).Value)
        {
            project.BackupPath.ShouldNotBeNull();
            File.Exists(project.BackupPath).ShouldBeTrue();
            Path.GetFileName(project.BackupPath).ShouldStartWith(ProjectFiles.BackupPrefix);
            project.Repository.GetPageContent(Ilsa).ShouldNotBeNull().Markdown.ShouldContain("[[7A1C4E90B3F2D615]]");
        }

        ManifestVersion().ShouldBe(ProjectFormat.CurrentVersion);
        Backups().Length.ShouldBe(1);
    }

    [Fact]
    public void Open_BackupHoldsTheProjectAsItWas()
    {
        var folder = Legacy(1);
        string backup;
        using (var project = ProjectStore.Open(folder, _clock, migrate: true).Value)
        {
            backup = project.BackupPath!;
        }

        using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={backup};Pooling=False");
        connection.Open();
        connection.ExecuteScalar<int>("PRAGMA user_version;").ShouldBe(1);
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM elements WHERE kind = 'page'").ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Open_CurrentFormat_NeedsNoConfirmationAndNoBackup()
    {
        var folder = Legacy(ProjectFormat.CurrentVersion);

        using (var project = ProjectStore.Open(folder, _clock, migrate: false).Value)
        {
            project.BackupPath.ShouldBeNull();
        }

        Backups().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void Open_MigrationFailsMidWay_RestoresTheOriginalProject(int failingStep)
    {
        var folder = Legacy(1);

        var result = ProjectStore.Open(folder, _clock, migrate: true, onMigrationStep: version =>
        {
            if (version == failingStep)
            {
                throw new InvalidOperationException("simulated failure");
            }
        });

        result.Error.Code.ShouldBe(StorageErrors.MigrationFailed);
        result.Error.Message.ShouldContain("simulated failure");
        ManifestVersion().ShouldBe(1);
        Backups().ShouldBeEmpty();
        File.Exists(_dir.Combine(ProjectFiles.Lock)).ShouldBeFalse();
        using var original = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dir.Combine(ProjectFiles.Database)};Pooling=False");
        original.Open();
        original.ExecuteScalar<int>("PRAGMA user_version;").ShouldBe(1);
        original.ExecuteScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        original.ExecuteScalar<int>("SELECT COUNT(*) FROM sqlite_master WHERE name = 'categories'").ShouldBe(0);
    }

    [Fact]
    public void Open_AfterAFailedMigration_CanBeRetried()
    {
        var folder = Legacy(1);
        ProjectStore.Open(folder, _clock, migrate: true, onMigrationStep: version =>
        {
            if (version == 2)
            {
                throw new InvalidOperationException("simulated failure");
            }
        }).IsSuccess.ShouldBeFalse();

        using var project = ProjectStore.Open(folder, _clock, migrate: true).Value;

        project.Manifest.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
        project.Repository.GetPageContent(Ilsa).ShouldNotBeNull();
    }

    [Fact]
    public void Open_MigrationThatLeavesAnInvalidProject_IsRolledBack()
    {
        // A project the doctor finds broken after the migration is not opened: here, a page without content.
        var folder = Legacy(1);
        using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={_dir.Combine(ProjectFiles.Database)};Pooling=False"))
        {
            connection.Open();
            connection.Execute("DELETE FROM page_contents WHERE page_id = @Id", new { Id = Ilsa.ToInt64() });
        }

        var result = ProjectStore.Open(folder, _clock, migrate: true);

        result.Error.Code.ShouldBe(StorageErrors.MigrationFailed);
        ManifestVersion().ShouldBe(1);
        Backups().ShouldBeEmpty();
    }
}
