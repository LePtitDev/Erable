using System.Security.Cryptography;
using System.Text;
using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Projects;
using Erable.Core.Properties;
using Erable.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

/// <summary>
/// Every published format version keeps a frozen project in <c>spec/projects/legacy/vN</c> (docs/testing.md §6):
/// it is opened, migrated to the current version and checked. We never break an existing user project.
/// </summary>
public sealed class LegacyProjectTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 8, 0, 0, TimeSpan.Zero);
    private static readonly ElementId Folder = ElementId.Parse("1E6AC1F0A0000001");
    private static readonly ElementId Ilsa = ElementId.Parse("3F2A9C14E07B5D21");
    private static readonly ElementId Orsen = ElementId.Parse("7A1C4E90B3F2D615");
    private static readonly ElementId Portrait = ElementId.Parse("1E6AC1F0A0000002");
    private static readonly ElementId Pnj = ElementId.Parse("1E6AC1F0A0000003");
    private static readonly ElementId Niveau = ElementId.Parse("1E6AC1F0A0000004");
    private static readonly byte[] PortraitBytes = Encoding.UTF8.GetBytes("legacy v1 blob\n");

    public static TheoryData<int> Versions =>
        [.. Directory.GetDirectories(RepositoryPaths.Combine("spec", "projects", "legacy"))
            .Select(d => int.Parse(Path.GetFileName(d)[1..], System.Globalization.CultureInfo.InvariantCulture))];

    [Theory]
    [MemberData(nameof(Versions))]
    public void Open_LegacyProject_MigratesAndKeepsContent(int version)
    {
        using var dir = TempDirectory.Create();
        FolderFingerprint.Copy(RepositoryPaths.Combine("spec", "projects", "legacy", $"v{version}"), dir.Path);

        using var project = ProjectStore.Open(dir.Path, new FakeTimeProvider(Now)).Value;

        project.Manifest.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
        (project.BackupPath is not null).ShouldBe(version < ProjectFormat.CurrentVersion);
        using var connection = project.Database.OpenConnection();
        connection.ExecuteScalar<string>("PRAGMA integrity_check;").ShouldBe("ok");
        connection.Query("PRAGMA foreign_key_check;").ShouldBeEmpty();
        project.Repository.GetElement(Ilsa).ShouldNotBeNull().Name.ShouldBe("Ilsa Varenne");
        project.Repository.GetPageContent(Ilsa).ShouldNotBeNull().Markdown.ShouldContain("[[7A1C4E90B3F2D615]]");
        project.Repository.GetIncomingReferences(Orsen).Select(r => r.SourceId).ShouldBe([Ilsa]);
        project.Repository.GetAttachments(Ilsa).Single().FileName.ShouldBe("portrait_ilsa.txt");
        project.Repository.LoadTree().FindAnomalies().ShouldBeEmpty();
        // The frozen projects are written by hand: their search text is not the analyzer's (a derived index, rebuilt by fix).
        ProjectDoctor.Diagnose(project, fix: false).Findings
            .Where(f => f.Severity != DoctorSeverity.Info && f.Code != DoctorCodes.IndexOutOfSync).Select(f => f.Code).ShouldBeEmpty();
        if (version >= 2)
        {
            // Structure (format v2): categories, definitions, values and their derived tables.
            var state = StructureState.Load(connection, null);
            state.CategoriesOf(Ilsa).ShouldBe([Pnj]);
            var niveau = state.ResolvePage(Ilsa).Single(p => p.Definition.Id == Niveau);
            niveau.Resolution.EffectiveValue.ShouldBe(new IntValue(72));
            niveau.Violations.Count.ShouldBe(1);
            state.ResolvePage(Orsen).Single(p => p.Definition.Id == Niveau).Origin.ShouldBe(ValueOrigin.Inherited);
        }
    }

    /// <summary>
    /// Writes <c>spec/projects/legacy/v{current}</c>. Run once when a format version is published, then never again:
    /// <c>dotnet test --project tests/Erable.Storage.Tests -- --filter-method *GenerateCurrentLegacyProject --explicit only</c>.
    /// </summary>
    [Fact(Explicit = true)]
    public void GenerateCurrentLegacyProject()
    {
        var target = RepositoryPaths.Combine("spec", "projects", "legacy", $"v{ProjectFormat.CurrentVersion}");
        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }

        using (var project = ProjectStore.Create(target, $"Projet figé v{ProjectFormat.CurrentVersion}", "fr", new FakeTimeProvider(Now)).Value)
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(PortraitBytes));
            var blob = Path.Combine(project.BlobsDirectory, sha[..2], sha[2..4], sha);
            Directory.CreateDirectory(Path.GetDirectoryName(blob)!);
            File.WriteAllBytes(blob, PortraitBytes);

            project.Database.Write((c, t) =>
            {
                Element E(ElementId id, ElementKind kind, ElementId parent, string name, long order) =>
                    new(id, kind, parent, name, order, 1, Now, KnownUsers.Local, Now, KnownUsers.Local, null);

                ProjectRepository.InsertElement(c, t, E(Folder, ElementKind.Folder, Roots.Pages.Id, "Personnages", 65536));
                ProjectRepository.InsertElement(c, t, E(Ilsa, ElementKind.Page, Folder, "Ilsa Varenne", 65536));
                ProjectRepository.InsertElement(c, t, E(Orsen, ElementKind.Page, Folder, "Capitaine Orsen", 131072));
                ProjectRepository.InsertElement(c, t, E(Portrait, ElementKind.Attachment, Ilsa, "portrait_ilsa.txt", 65536));
                ProjectRepository.InsertAttachment(c, t, new AttachmentRecord(Portrait, Ilsa, "portrait_ilsa.txt", "text/plain", PortraitBytes.Length, sha));
                ProjectRepository.UpsertPageContent(c, t, Ilsa, "## Présentation\n\nAlliée de [[7A1C4E90B3F2D615]].\n\n![[1E6AC1F0A0000002]]\n", "Présentation Alliée de Capitaine Orsen");
                ProjectRepository.UpsertPageContent(c, t, Orsen, "Garde de Hautbois.\n", "Garde de Hautbois");
                ProjectRepository.ReplaceReferences(c, t, Ilsa,
                [
                    new StoredReference(Ilsa, Orsen, null, false, 28),
                    new StoredReference(Ilsa, Portrait, null, true, 50),
                ]);

                // Format v2: a category with a default, a property with a rule, values and their derived tables.
                ProjectRepository.InsertElement(c, t, E(Pnj, ElementKind.Category, Roots.Categories.Id, "PNJ", 65536));
                ProjectRepository.InsertElement(c, t, E(Niveau, ElementKind.PropertyDefinition, Roots.Properties.Id, "Niveau", 65536));
                StructureRepository.UpsertCategory(c, t, new CategoryRecord(Pnj, ElementId.None, "Personnage non joueur."));
                StructureRepository.UpsertDefinition(c, t, new PropertyDefinition(
                    Niveau, "Niveau", "niveau", "", PropertyType.Int, null, [new NumberRule(RuleOp.LessOrEqual, 60)], []));
                StructureRepository.SetValue(c, t, Pnj, Niveau, new IntValue(10));
                StructureRepository.SetValue(c, t, Ilsa, Niveau, new IntValue(72));
                StructureRepository.ReplacePageCategories(c, t, Ilsa, [Pnj]);
                StructureRepository.ReplacePageCategories(c, t, Orsen, [Pnj]);
                StructureState.Load(c, null).WriteDerived(c, t, null, null);
                return 0;
            });
        }

        var manifest = ManifestFile.Read(target).Value with { ProjectId = ElementId.Parse("1E6AC1F0A00000FF") };
        ManifestFile.Write(target, manifest);
    }
}
