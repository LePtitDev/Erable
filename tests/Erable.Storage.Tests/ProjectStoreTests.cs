using System.Text.Json.Nodes;
using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Projects;
using Erable.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

public sealed class ProjectStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);
    private readonly FakeTimeProvider _clock = new(Now);

    [Fact]
    public void Create_ThenReopen_KeepsManifestAndRoots()
    {
        using var dir = TempDirectory.Create();
        ProjectManifest created;
        using (var project = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value)
        {
            created = project.Manifest;
        }

        using var reopened = ProjectStore.Open(dir.Path, _clock).Value;

        reopened.Manifest.ShouldBe(created);
        created.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
        created.ContentLanguage.ShouldBe("fr");
        created.CreatedAt.ShouldBe(Now);
        reopened.Repository.LoadTree().Nodes.Select(n => n.Id).ShouldBe(Roots.All.Select(r => r.Id), ignoreOrder: true);
        IntegrityCheck(reopened).ShouldBe("ok");
    }

    [Fact]
    public void Create_ThenReopen_KeepsElementsAndContent()
    {
        using var dir = TempDirectory.Create();
        var page = ElementId.NewRandom();
        using (var project = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value)
        {
            project.Database.Write((c, t) =>
            {
                ProjectRepository.InsertElement(c, t, new Element(page, ElementKind.Page, Roots.Pages.Id, "Ilsa Varenne", 1, 1, Now, "local", Now, "local", null));
                ProjectRepository.UpsertPageContent(c, t, page, "# Ilsa\n\nVoir [[7A1C4E90B3F2D615]].", "Ilsa Voir");
                ProjectRepository.ReplaceReferences(c, t, page, [new StoredReference(page, ElementId.Parse("7A1C4E90B3F2D615"), null, false, 14)]);
                return 0;
            });
        }

        using var reopened = ProjectStore.Open(dir.Path, _clock).Value;

        var element = reopened.Repository.GetElement(page).ShouldNotBeNull();
        element.Name.ShouldBe("Ilsa Varenne");
        element.CreatedAt.ShouldBe(Now);
        reopened.Repository.GetPageContent(page).ShouldNotBeNull().Markdown.ShouldBe("# Ilsa\n\nVoir [[7A1C4E90B3F2D615]].");
        reopened.Repository.GetIncomingReferences(ElementId.Parse("7A1C4E90B3F2D615")).Single().SourceId.ShouldBe(page);
        IntegrityCheck(reopened).ShouldBe("ok");
    }

    [Fact]
    public void Dispose_Project_LeavesNoWalNorLockFile()
    {
        using var dir = TempDirectory.Create();
        ProjectStore.Create(dir.Path, "Atlas", "en", _clock).Value.Dispose();

        Directory.EnumerateFiles(dir.Path).Select(Path.GetFileName).ShouldBe(["erable.db", "erable.json"], ignoreOrder: true);
    }

    [Fact]
    public void Open_FolderWithoutManifest_FailsAndWritesNothing()
    {
        using var dir = TempDirectory.Create();
        File.WriteAllText(dir.Combine("notes.txt"), "pas un projet");
        var before = FolderFingerprint.Of(dir.Path);

        var result = ProjectStore.Open(dir.Path, _clock);

        result.Error.Code.ShouldBe(StorageErrors.NotAProject);
        FolderFingerprint.Of(dir.Path).ShouldBe(before);
        ProjectStore.Inspect(dir.Path).Status.ShouldBe(FolderStatus.NotAProject);
    }

    [Fact]
    public void Open_NewerFormat_FailsAndLeavesFilesUnchanged()
    {
        using var dir = TempDirectory.Create();
        ProjectStore.Create(dir.Path, "Futur", "fr", _clock).Value.Dispose();
        var manifest = JsonNode.Parse(File.ReadAllText(dir.Combine("erable.json")))!;
        manifest["formatVersion"] = ProjectFormat.CurrentVersion + 1;
        File.WriteAllText(dir.Combine("erable.json"), manifest.ToJsonString());
        var before = FolderFingerprint.Of(dir.Path);

        var result = ProjectStore.Open(dir.Path, _clock);

        result.Error.Code.ShouldBe(StorageErrors.FormatTooNew);
        FolderFingerprint.Of(dir.Path).ShouldBe(before);
        ProjectStore.Inspect(dir.Path).Status.ShouldBe(FolderStatus.TooNew);
    }

    [Fact]
    public void Open_DatabaseSchemaNewerThanManifest_FailsAsTooNew()
    {
        using var dir = TempDirectory.Create();
        using (var project = ProjectStore.Create(dir.Path, "Futur", "fr", _clock).Value)
        {
            using var connection = project.Database.OpenConnection();
            connection.Execute($"PRAGMA user_version = {ProjectFormat.CurrentVersion + 1};");
        }

        ProjectStore.Open(dir.Path, _clock).Error.Code.ShouldBe(StorageErrors.FormatTooNew);
        File.Exists(dir.Combine(ProjectFiles.Lock)).ShouldBeFalse();
    }

    [Fact]
    public void Open_AlreadyOpen_IsDetectedAsLocked()
    {
        using var dir = TempDirectory.Create();
        using var first = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value;

        ProjectStore.Open(dir.Path, _clock).Error.Code.ShouldBe(StorageErrors.Locked);
    }

    [Fact]
    public void Open_AfterFirstInstanceClosed_Succeeds()
    {
        using var dir = TempDirectory.Create();
        ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value.Dispose();
        ProjectStore.Open(dir.Path, _clock).Value.Dispose();

        using var again = ProjectStore.Open(dir.Path, _clock).Value;

        again.Manifest.Name.ShouldBe("Atlas");
    }

    [Fact]
    public void Create_WhereProjectExists_Fails()
    {
        using var dir = TempDirectory.Create();
        ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value.Dispose();

        ProjectStore.Create(dir.Path, "Encore", "fr", _clock).Error.Code.ShouldBe(StorageErrors.AlreadyExists);
    }

    [Theory]
    [InlineData("", "fr", ErrorCodes.NameEmpty)]
    [InlineData("Atlas", "klingon", StorageErrors.InvalidLanguage)]
    public void Create_InvalidInput_FailsWithoutCreatingFolder(string name, string language, string code)
    {
        using var dir = TempDirectory.Create();
        var folder = dir.Combine("nouveau");

        ProjectStore.Create(folder, name, language, _clock).Error.Code.ShouldBe(code);
        Directory.Exists(folder).ShouldBeFalse();
    }

    [Fact]
    public void Create_MissingFolder_CreatesIt()
    {
        using var dir = TempDirectory.Create();
        var folder = dir.Combine("Mes projets", "Atlas");

        using var project = ProjectStore.Create(folder, "Atlas", "de", _clock).Value;

        ProjectStore.Inspect(folder).Status.ShouldBe(FolderStatus.Project);
        Directory.Exists(Path.Combine(folder, "blobs")).ShouldBeTrue();
    }

    [Fact]
    public void Inspect_MissingFolder_IsMissing()
    {
        using var dir = TempDirectory.Create();

        ProjectStore.Inspect(dir.Combine("absent")).Status.ShouldBe(FolderStatus.Missing);
    }

    [Fact]
    public void Open_InvalidManifest_FailsAsInvalid()
    {
        using var dir = TempDirectory.Create();
        File.WriteAllText(dir.Combine("erable.json"), "{ pas du json");

        ProjectStore.Open(dir.Path, _clock).Error.Code.ShouldBe(StorageErrors.InvalidManifest);
        ProjectStore.Inspect(dir.Path).Status.ShouldBe(FolderStatus.Invalid);
    }

    [Fact]
    public void Database_Pragmas_AreWalAndForeignKeys()
    {
        using var dir = TempDirectory.Create();
        using var project = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value;
        using var connection = project.Database.OpenConnection();

        connection.ExecuteScalar<string>("PRAGMA journal_mode;").ShouldBe("wal");
        connection.ExecuteScalar<long>("PRAGMA foreign_keys;").ShouldBe(1);
        connection.ExecuteScalar<long>("PRAGMA busy_timeout;").ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Write_ForeignKeyViolation_RollsBackWholeTransaction()
    {
        using var dir = TempDirectory.Create();
        using var project = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value;
        var page = ElementId.NewRandom();

        Should.Throw<Microsoft.Data.Sqlite.SqliteException>(() => project.Database.Write((c, t) =>
        {
            ProjectRepository.InsertElement(c, t, new Element(page, ElementKind.Page, Roots.Pages.Id, "Ok", 1, 1, Now, "local", Now, "local", null));
            ProjectRepository.InsertElement(c, t, new Element(ElementId.NewRandom(), ElementKind.Page, ElementId.NewRandom(), "Orpheline", 1, 1, Now, "local", Now, "local", null));
            return 0;
        }));

        project.Repository.GetElement(page).ShouldBeNull();
    }

    [Fact]
    public void GetElements_ManyIds_ReturnsKnownOnes()
    {
        using var dir = TempDirectory.Create();
        using var project = ProjectStore.Create(dir.Path, "Atlas", "fr", _clock).Value;
        var unknown = Enumerable.Range(0, 500).Select(_ => ElementId.NewRandom()).ToList();

        var found = project.Repository.GetElements([.. unknown, Roots.Pages.Id, Roots.Links.Id]);

        found.Select(e => e.Id).ShouldBe([Roots.Pages.Id, Roots.Links.Id], ignoreOrder: true);
    }

    private static string IntegrityCheck(OpenedProject project)
    {
        using var connection = project.Database.OpenConnection();
        return connection.ExecuteScalar<string>("PRAGMA integrity_check;")!;
    }
}
