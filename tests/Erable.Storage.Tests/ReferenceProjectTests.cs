using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Erable.Testing.Reference;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>
/// The committed reference project <c>spec/projects/chroniques/</c> (docs/features.md §2) must stay what its C#
/// description says. After a change of the description or of the format, regenerate it:
/// <c>dotnet test --project tests/Erable.Storage.Tests -- --filter-method *GenerateChroniques --explicit only</c>.
/// </summary>
public sealed class ReferenceProjectTests
{
    [Fact(Explicit = true)]
    public void GenerateChroniques()
    {
        var target = Chroniques.CommittedDirectory;
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        using (Chroniques.Write(target))
        {
        }

        File.Exists(Path.Combine(target, ProjectFiles.Lock)).ShouldBeFalse();
    }

    [Fact]
    public void Committed_MatchesTheDescription()
    {
        using var committed = TempDirectory.Create();
        using var generated = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, committed.Path);

        string expected;
        using (var project = Chroniques.Write(generated.Path))
        {
            expected = Dump(project);
        }

        using var opened = ProjectStore.Open(committed.Path, new FakeTimeProvider(Chroniques.Now)).Value;
        Dump(opened).ShouldBe(expected, "spec/projects/chroniques is stale: run the GenerateChroniques test (see the class comment).");
    }

    [Fact]
    public void Committed_IsHealthy()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);

        using var project = ProjectStore.Open(dir.Path, new FakeTimeProvider(Chroniques.Now)).Value;

        project.BackupPath.ShouldBeNull("the reference project is always at the current format");
        project.Manifest.ProjectId.ShouldBe(Chroniques.ProjectId);
        using var connection = project.Database.OpenConnection();
        connection.ExecuteScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        project.Repository.LoadTree().FindAnomalies().ShouldBeEmpty();
        var unknownTargets = connection.Query<long>("SELECT DISTINCT target_id FROM refs WHERE target_id NOT IN (SELECT id FROM elements)");
        unknownTargets.ShouldBeEmpty();
        foreach (var sha in connection.Query<string>("SELECT sha256 FROM attachments"))
        {
            project.Blobs.Exists(sha).ShouldBeTrue();
        }
    }

    [Fact]
    public void Committed_HasTheFrozenIdsAndValuesOfTheFeatures()
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(Chroniques.CommittedDirectory, dir.Path);
        using var project = ProjectStore.Open(dir.Path, new FakeTimeProvider(Chroniques.Now)).Value;
        var repository = project.Repository;

        var ilsa = repository.GetElement(Chroniques.Ilsa).ShouldNotBeNull();
        ilsa.Name.ShouldBe("Ilsa Varenne");
        ilsa.UpdatedAt.ShouldBe(Chroniques.Now.AddHours(-2));
        ilsa.UpdatedBy.ShouldBe("camille");
        repository.GetElement(Chroniques.Orsen).ShouldNotBeNull().Name.ShouldBe("Capitaine Orsen");
        repository.GetOutgoingReferences(Chroniques.Ilsa).Select(r => r.TargetId).ShouldBe(
            [Chroniques.Hautbois, Chroniques.OrdreDuGivre, Chroniques.Orsen, Chroniques.IdOf("portrait_ilsa.png"), Chroniques.IdOf("carte_hautbois.webp"), Chroniques.ColGeleQuete]);
        repository.GetAttachments(Chroniques.Hautbois).Select(a => (a.FileName, a.Mime)).ShouldBe(
            [("carte_hautbois.webp", "image/webp"), ("bande_annonce.mp4", "video/mp4"), ("plans.zip", "application/zip"), ("blason.svg", "image/svg+xml")]);
        var tree = repository.LoadTree();
        tree.Children(Chroniques.Ilsa).Where(n => n.Kind == ElementKind.Page).Select(n => n.Name).ShouldBe(["Dialogues", "Inventaire"]);
    }

    [Fact]
    public void Assets_StaySmall()
    {
        var total = Directory.EnumerateFiles(Chroniques.CommittedDirectory, "*", SearchOption.AllDirectories)
            .Where(f => f.Contains(ProjectFiles.Blobs, StringComparison.Ordinal))
            .Sum(f => new FileInfo(f).Length);

        total.ShouldBeLessThan(1024 * 1024);
    }

    /// <summary>A readable, order-stable text view of everything the description controls.</summary>
    private static string Dump(OpenedProject project)
    {
        using var connection = project.Database.OpenConnection();
        var lines = new List<string> { $"project {project.Manifest.ProjectId} {project.Manifest.Name} {project.Manifest.ContentLanguage} {project.Manifest.FormatVersion}" };
        lines.AddRange(connection.Query<string>(
            """
            SELECT printf('%016X', id) || ' ' || kind || ' ' || ifnull(printf('%016X', parent_id), '-') || ' ' || name || ' ' || sort_order
                || ' ' || revision || ' ' || created_at || ' ' || created_by || ' ' || updated_at || ' ' || updated_by
            FROM elements ORDER BY id
            """));
        lines.AddRange(connection.Query<string>("SELECT printf('%016X', page_id) || char(10) || markdown || char(10) || plain_text FROM page_contents ORDER BY page_id"));
        lines.AddRange(connection.Query<string>("SELECT printf('%016X %016X %d %d ', source_id, target_id, is_embed, position) || ifnull(anchor, '') FROM refs ORDER BY source_id, position"));
        lines.AddRange(connection.Query<string>("SELECT printf('%016X ', id) || file_name || ' ' || mime || ' ' || size || ' ' || sha256 FROM attachments ORDER BY id"));
        lines.AddRange(project.Blobs.List().Order(StringComparer.Ordinal));
        return string.Join('\n', lines);
    }
}
