using Dapper;
using Erable.Core;
using Erable.Core.Projects;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Data.Sqlite;

namespace Erable.Storage.Tests;

/// <summary>
/// One deliberately corrupted project per anomaly (docs/testing.md §6). Each corruption is a recipe applied to a small
/// healthy project, so the corrupted projects follow the schema without being versioned as binary files.
/// </summary>
public sealed class ProjectDoctorTests : IDisposable
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9, 9];

    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly ProjectBuilder _builder = Project.Build()
        .WithFolder("Lieux")
        .WithPage("Ilsa", markdown: "Voir [[@Hautbois]] et [[@Col]].\n\n![[@portrait.png]]")
        .WithPage("Hautbois", under: "Lieux", markdown: "Port.")
        .WithPage("Col", under: "Lieux", markdown: "Montagne.")
        .WithAttachment("portrait.png", "Ilsa", Png);

    public ProjectDoctorTests()
    {
        using (ProjectSeeder.Create(_builder, _dir.Path))
        {
        }
    }

    public void Dispose() => _dir.Dispose();

    private long Id(string name) => _builder.IdOf(name).ToInt64();

    private string BlobOf(string name)
    {
        using var connection = Connect();
        return connection.ExecuteScalar<string>("SELECT sha256 FROM attachments WHERE id = @Id", new { Id = Id(name) })!;
    }

    private string BlobPath(string sha) => Path.Combine(_dir.Path, "blobs", sha[..2], sha[2..4], sha);

    /// <summary>Raw access, foreign keys off: how a corruption gets in.</summary>
    private SqliteConnection Connect()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(_dir.Path, ProjectFiles.Database),
            Pooling = false,
            ForeignKeys = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private void Corrupt(string sql, object? parameters = null)
    {
        using var connection = Connect();
        connection.Execute(sql, parameters);
    }

    private DoctorReport Diagnose(bool fix = false) => ProjectDoctor.Diagnose(_dir.Path, fix);

    private static string[] Codes(DoctorReport report) => [.. report.Findings.Select(f => f.Code).Distinct()];

    [Fact]
    public void Diagnose_HealthyProject_NoFinding()
    {
        var report = Diagnose();

        report.Findings.ShouldBeEmpty();
        report.Healthy.ShouldBeTrue();
        report.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
    }

    [Fact]
    public void Diagnose_ReferenceProject_IsHealthy()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);

        var report = ProjectDoctor.Diagnose(dir.Path, fix: false);

        report.Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Diagnose_DamagedDatabaseFile_ReportsIntegrity()
    {
        long page;
        using (var connection = Connect())
        {
            page = connection.ExecuteScalar<long>("SELECT rootpage FROM sqlite_master WHERE name = 'ix_refs_target'");
            connection.ExecuteScalar<long>("PRAGMA page_size");
        }

        SqliteConnection.ClearAllPools();
        using (var file = new FileStream(Path.Combine(_dir.Path, ProjectFiles.Database), FileMode.Open, FileAccess.ReadWrite))
        {
            file.Seek((page - 1) * 4096, SeekOrigin.Begin);
            file.Write(new byte[4096]);
        }

        var report = Diagnose();

        Codes(report).ShouldBe([DoctorCodes.Integrity]);
        report.Healthy.ShouldBeFalse();
    }

    [Fact]
    public void Diagnose_RowWithoutParentRow_ReportsForeignKey()
    {
        Corrupt("INSERT INTO refs (source_id, target_id, anchor, is_embed, position) VALUES (12345, 1, NULL, 0, 0)");

        Codes(Diagnose()).ShouldBe([DoctorCodes.ForeignKey]);
    }

    [Fact]
    public void Diagnose_LiveChildOfDeletedFolder_ReportsMissingParent()
    {
        Corrupt("UPDATE elements SET deleted_at = '2026-10-13T08:00:00.000Z' WHERE id = @Id", new { Id = Id("Lieux") });

        var report = Diagnose();

        report.Findings.Where(f => f.Code == DoctorCodes.TreeMissingParent).Select(f => f.ElementId)
            .ShouldBe([_builder.IdOf("Hautbois"), _builder.IdOf("Col")], ignoreOrder: true);
    }

    [Fact]
    public void Diagnose_ParentCycle_ReportsCycle()
    {
        Corrupt("UPDATE elements SET parent_id = @Col WHERE id = @Hautbois; UPDATE elements SET parent_id = @Hautbois WHERE id = @Col",
            new { Col = Id("Col"), Hautbois = Id("Hautbois") });

        Codes(Diagnose()).ShouldContain(DoctorCodes.TreeCycle);
    }

    [Fact]
    public void Diagnose_PageUnderCategoriesRoot_ReportsOutsideOfRoot()
    {
        Corrupt("UPDATE elements SET parent_id = 2 WHERE id = @Id", new { Id = Id("Col") });

        Diagnose().Findings.Single().ShouldBe(new DoctorFinding(
            DoctorCodes.TreeOutsideOfRoot, DoctorSeverity.Error, "The parent cannot contain this kind of element.", _builder.IdOf("Col")));
    }

    [Fact]
    public void Diagnose_PageWithoutContent_ReportsContentMissing()
    {
        Corrupt("DELETE FROM page_contents WHERE page_id = @Id", new { Id = Id("Col") });

        Codes(Diagnose()).ShouldBe([DoctorCodes.PageContentMissing]);
    }

    [Fact]
    public void Diagnose_ReferenceIndexOutOfSync_FixRebuildsIt()
    {
        Corrupt("DELETE FROM refs WHERE source_id = @Id", new { Id = Id("Ilsa") });

        var report = Diagnose(fix: true);

        report.Findings.Single().ShouldBe(new DoctorFinding(DoctorCodes.IndexOutOfSync, DoctorSeverity.Warning,
            "The search text or reference index differs from the Markdown.", _builder.IdOf("Ilsa"), Fixable: true, Fixed: true));
        report.Healthy.ShouldBeTrue();
        Diagnose().Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Diagnose_ReferenceToDeletedElement_IsOnlyInformation()
    {
        Corrupt("UPDATE elements SET deleted_at = '2026-10-13T08:00:00.000Z' WHERE id = @Id", new { Id = Id("Col") });

        var report = Diagnose();

        report.Findings.Single().Code.ShouldBe(DoctorCodes.ReferenceToDeleted);
        report.Findings.Single().Severity.ShouldBe(DoctorSeverity.Info);
        report.Healthy.ShouldBeTrue();
    }

    [Fact]
    public void Diagnose_ReferenceToUnknownElement_IsOnlyInformation()
    {
        Corrupt("UPDATE page_contents SET markdown = markdown || ' [[3F2A9C14E07B5D21]]', plain_text = plain_text WHERE page_id = @Id", new { Id = Id("Col") });
        ProjectDoctor.Diagnose(_dir.Path, fix: true);

        var report = Diagnose();

        report.Findings.Single().Code.ShouldBe(DoctorCodes.ReferenceToUnknown);
        report.Healthy.ShouldBeTrue();
    }

    [Fact]
    public void Diagnose_AttachmentFileDeleted_ReportsBlobMissing()
    {
        File.Delete(BlobPath(BlobOf("portrait.png")));

        Codes(Diagnose()).ShouldBe([DoctorCodes.BlobMissing]);
    }

    [Fact]
    public void Diagnose_AttachmentFileAltered_ReportsBlobCorrupted()
    {
        File.WriteAllBytes(BlobPath(BlobOf("portrait.png")), [1, 2, 3]);

        Codes(Diagnose()).ShouldBe([DoctorCodes.BlobCorrupted]);
    }

    [Fact]
    public void Diagnose_RevisionFileDeleted_ReportsMissingForRevision()
    {
        var sha = BlobOf("portrait.png");
        Corrupt("DELETE FROM attachments WHERE id = @Id", new { Id = Id("portrait.png") });
        File.Delete(BlobPath(sha));

        Codes(Diagnose()).ShouldBe([DoctorCodes.RevisionBlobMissing]);
    }

    [Fact]
    public void Diagnose_UnreferencedFile_FixDeletesIt()
    {
        var orphan = new string('a', 64);
        Directory.CreateDirectory(Path.GetDirectoryName(BlobPath(orphan))!);
        File.WriteAllBytes(BlobPath(orphan), [7]);

        var report = Diagnose(fix: true);

        report.Findings.Single().ShouldBe(new DoctorFinding(DoctorCodes.BlobOrphan, DoctorSeverity.Warning,
            "No attachment or revision uses this file.", Blob: orphan, Fixable: true, Fixed: true));
        File.Exists(BlobPath(orphan)).ShouldBeFalse();
        File.Exists(BlobPath(BlobOf("portrait.png"))).ShouldBeTrue();
    }

    [Fact]
    public void Diagnose_LeftoverTemporaryFile_FixDeletesIt()
    {
        Directory.CreateDirectory(Path.Combine(_dir.Path, "blobs", "tmp"));
        File.WriteAllBytes(Path.Combine(_dir.Path, "blobs", "tmp", "partial"), [1]);

        Diagnose(fix: true).Findings.Single().Code.ShouldBe(DoctorCodes.BlobTemporary);

        Diagnose().Findings.ShouldBeEmpty();
    }

    [Fact]
    public void Diagnose_ManifestAndSchemaDisagree_ReportsMismatch()
    {
        var manifest = ManifestFile.Read(_dir.Path).Value;
        ManifestFile.Write(_dir.Path, manifest with { FormatVersion = ProjectFormat.CurrentVersion + 1 });

        Codes(Diagnose()).ShouldBe([DoctorCodes.FormatMismatch]);
    }

    [Fact]
    public void Diagnose_NewerSchema_ReportsTooNewWithoutChangingFiles()
    {
        Corrupt($"PRAGMA user_version = {ProjectFormat.CurrentVersion + 1}");
        var before = FolderFingerprint.Of(_dir.Path);

        Codes(Diagnose()).ShouldContain(DoctorCodes.FormatTooNew);

        FolderFingerprint.Of(_dir.Path).ShouldBe(before);
    }

    [Fact]
    public void Diagnose_UnreadableManifest_ReportsManifestInvalid()
    {
        File.WriteAllText(Path.Combine(_dir.Path, ProjectFiles.Manifest), "{ not json");

        Codes(Diagnose()).ShouldBe([DoctorCodes.ManifestInvalid]);
    }

    [Fact]
    public void Diagnose_ProjectOpenElsewhere_ReportsLocked()
    {
        using var held = ProjectLock.Acquire(_dir.Path).Value;

        Codes(Diagnose()).ShouldBe([DoctorCodes.ProjectLocked]);
    }

    [Fact]
    public void Diagnose_WithoutFix_ChangesNothing()
    {
        Corrupt("DELETE FROM refs WHERE source_id = @Id", new { Id = Id("Ilsa") });
        SqliteConnection.ClearAllPools();
        var before = FolderFingerprint.Of(_dir.Path);

        Diagnose();

        SqliteConnection.ClearAllPools();
        FolderFingerprint.Of(_dir.Path).ShouldBe(before);
    }
}
