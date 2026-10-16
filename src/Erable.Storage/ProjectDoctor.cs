using System.Security.Cryptography;
using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Markdown;
using Erable.Core.Tree;
using Microsoft.Data.Sqlite;

namespace Erable.Storage;

public enum DoctorSeverity
{
    /// <summary>A legitimate state worth knowing (a reference to a deleted element).</summary>
    Info,

    /// <summary>Wrong but harmless for the content, usually repairable (<c>--fix</c>).</summary>
    Warning,

    /// <summary>Data is damaged or inconsistent.</summary>
    Error,
}

/// <summary>One anomaly found by <see cref="ProjectDoctor"/>. <see cref="Code"/> is stable (scripts and tests rely on it).</summary>
public sealed record DoctorFinding(
    string Code, DoctorSeverity Severity, string Message, ElementId? ElementId = null, string? Blob = null, bool Fixable = false, bool Fixed = false);

public sealed record DoctorReport(string Folder, int? FormatVersion, IReadOnlyList<DoctorFinding> Findings)
{
    /// <summary>No warning or error left (fixed ones and infos do not count).</summary>
    public bool Healthy => Findings.All(f => f.Fixed || f.Severity == DoctorSeverity.Info);
}

/// <summary>Stable codes of <see cref="DoctorFinding"/>.</summary>
public static class DoctorCodes
{
    public const string ManifestInvalid = "manifest.invalid";
    public const string ProjectLocked = "project.locked";
    public const string FormatMismatch = "format.mismatch";
    public const string FormatTooNew = "format.tooNew";
    public const string FormatOutdated = "format.outdated";
    public const string Integrity = "sqlite.integrity";
    public const string ForeignKey = "sqlite.foreignKey";
    public const string TreeMissingParent = "tree.missingParent";
    public const string TreeCycle = "tree.cycle";
    public const string TreeDetached = "tree.detached";
    public const string TreeOutsideOfRoot = "tree.outsideOfRoot";
    public const string PageContentMissing = "page.contentMissing";
    public const string IndexOutOfSync = "index.outOfSync";
    public const string ReferenceToDeleted = "ref.deletedTarget";
    public const string ReferenceToUnknown = "ref.unknownTarget";
    public const string BlobMissing = "blob.missing";
    public const string BlobCorrupted = "blob.corrupted";
    public const string RevisionBlobMissing = "blob.missingForRevision";
    public const string BlobOrphan = "blob.orphan";
    public const string BlobTemporary = "blob.temporary";
    public const string CategoryCycle = "category.cycle";
    public const string StructureRowMissing = "structure.rowMissing";
    public const string ValueInvalid = "value.invalid";
    public const string ValueUnknownProperty = "value.unknownProperty";
    public const string DerivedOutOfSync = "derived.outOfSync";

    /// <summary>The full-text index (<c>search_fts</c>) differs from the names and texts it indexes, or is damaged.</summary>
    public const string SearchOutOfSync = "search.outOfSync";

    /// <summary>A link whose source, target or type does not exist or is not of the right kind.</summary>
    public const string LinkDangling = "link.dangling";
}

/// <summary>
/// Integrity diagnosis of a project (docs/testing.md §6): SQLite, format version, tree, derived indexes, references and
/// blobs. <c>fix</c> only performs safe repairs: rebuilding a derived index, deleting an unreferenced blob or a leftover
/// temporary file. User content is never deleted.
/// </summary>
public static class ProjectDoctor
{
    private const int MaxIntegrityMessages = 20;

    /// <summary>Diagnoses a closed project folder (the <c>erable doctor</c> command): takes the lock, never migrates.</summary>
    public static DoctorReport Diagnose(string folder, bool fix)
    {
        ArgumentNullException.ThrowIfNull(folder);
        folder = Path.GetFullPath(folder);
        var findings = new List<DoctorFinding>();
        var manifest = ManifestFile.Read(folder);
        if (!manifest.IsSuccess)
        {
            findings.Add(new(DoctorCodes.ManifestInvalid, DoctorSeverity.Error, manifest.Error.Message));
            return new DoctorReport(folder, null, findings);
        }

        var databasePath = Path.Combine(folder, ProjectFiles.Database);
        if (!File.Exists(databasePath))
        {
            findings.Add(new(DoctorCodes.ManifestInvalid, DoctorSeverity.Error, $"{ProjectFiles.Database} is missing."));
            return new DoctorReport(folder, manifest.Value.FormatVersion, findings);
        }

        var acquired = ProjectLock.Acquire(folder);
        if (!acquired.IsSuccess)
        {
            findings.Add(new(DoctorCodes.ProjectLocked, DoctorSeverity.Error, acquired.Error.Message));
            return new DoctorReport(folder, manifest.Value.FormatVersion, findings);
        }

        using var project = new OpenedProject(folder, manifest.Value, ProjectDatabase.Open(databasePath), acquired.Value, backupPath: null);
        return Diagnose(project, fix, closed: true);
    }

    /// <summary>Diagnoses a project open in this process (test hook); repairs go through the write gate.</summary>
    public static DoctorReport Diagnose(OpenedProject project, bool fix) => Diagnose(project, fix, closed: false);

    private static DoctorReport Diagnose(OpenedProject project, bool fix, bool closed)
    {
        ArgumentNullException.ThrowIfNull(project);
        var findings = new List<DoctorFinding>();
        int schema;
        try
        {
            schema = project.Database.ReadSchemaVersion();
            if (!CheckFormat(project, schema, findings) || !CheckIntegrity(project, findings))
            {
                return Report(project, findings);
            }
        }
        catch (SqliteException ex)
        {
            findings.Add(new(DoctorCodes.Integrity, DoctorSeverity.Error, ex.Message));
            return Report(project, findings);
        }

        CheckTree(project, findings);
        CheckPages(project, fix, findings);
        CheckStructure(project, fix, findings);
        CheckSearch(project, fix, findings);
        CheckBlobs(project, fix, closed, findings);
        return Report(project, findings);
    }

    private static DoctorReport Report(OpenedProject project, List<DoctorFinding> findings) =>
        new(project.Folder, project.Manifest.FormatVersion, [.. findings
            .OrderBy(f => f.Code, StringComparer.Ordinal)
            .ThenBy(f => f.ElementId?.Value ?? 0)
            .ThenBy(f => f.Blob, StringComparer.Ordinal)]);

    /// <summary>Returns false when the schema is not the current one: the other checks would be meaningless.</summary>
    private static bool CheckFormat(OpenedProject project, int schema, List<DoctorFinding> findings)
    {
        if (project.Manifest.FormatVersion != schema)
        {
            findings.Add(new(DoctorCodes.FormatMismatch, DoctorSeverity.Error,
                $"erable.json says format {project.Manifest.FormatVersion}, the database is at version {schema}."));
        }

        if (schema > ProjectFormat.CurrentVersion)
        {
            findings.Add(new(DoctorCodes.FormatTooNew, DoctorSeverity.Error,
                $"Format {schema} is newer than this application ({ProjectFormat.CurrentVersion}); update the application."));
            return false;
        }

        if (schema < ProjectFormat.CurrentVersion)
        {
            findings.Add(new(DoctorCodes.FormatOutdated, DoctorSeverity.Warning,
                $"Format {schema} must be migrated to {ProjectFormat.CurrentVersion}: open the project in the application."));
            return false;
        }

        return true;
    }

    private static bool CheckIntegrity(OpenedProject project, List<DoctorFinding> findings)
    {
        using var connection = project.Database.OpenConnection();
        // A pooled connection may hold a stale picture of the full-text index after a write by another connection, and
        // PRAGMA integrity_check would report it as corrupted. Reading it through FTS5 itself refreshes the picture; a real
        // corruption still shows up in the pragma.
        try
        {
            connection.Execute("INSERT INTO search_fts (search_fts) VALUES ('integrity-check');");
        }
        catch (SqliteException)
        {
        }

        var messages = connection.Query<string>($"PRAGMA integrity_check({MaxIntegrityMessages});").ToList();
        if (messages is not ["ok"])
        {
            findings.AddRange(messages.Select(m => new DoctorFinding(DoctorCodes.Integrity, DoctorSeverity.Error, m)));
            return false;
        }

        foreach (var row in connection.Query<(string Table, long RowId, string Parent)>("PRAGMA foreign_key_check;"))
        {
            findings.Add(new(DoctorCodes.ForeignKey, DoctorSeverity.Error,
                $"Row {row.RowId} of {row.Table} references a missing row of {row.Parent}."));
        }

        return true;
    }

    private static void CheckTree(OpenedProject project, List<DoctorFinding> findings)
    {
        foreach (var anomaly in project.Repository.LoadTree().FindAnomalies())
        {
            var (code, message) = anomaly.Kind switch
            {
                TreeAnomalyKind.MissingParent => (DoctorCodes.TreeMissingParent, "The parent does not exist or is deleted."),
                TreeAnomalyKind.Cycle => (DoctorCodes.TreeCycle, "The element is part of a parent cycle."),
                TreeAnomalyKind.Detached => (DoctorCodes.TreeDetached, "The chain of parents does not reach a root."),
                TreeAnomalyKind.OutsideOfRoot => (DoctorCodes.TreeOutsideOfRoot, "The parent cannot contain this kind of element."),
                _ => throw new ArgumentOutOfRangeException(nameof(findings), anomaly.Kind, null),
            };
            findings.Add(new(code, DoctorSeverity.Error, message, anomaly.Id));
        }
    }

    /// <summary>Content rows, derived indexes (plain text, references) and the state of referenced targets.</summary>
    private static void CheckPages(OpenedProject project, bool fix, List<DoctorFinding> findings)
    {
        using var connection = project.Database.OpenConnection();
        var pages = connection.Query<(long Id, string? Markdown, string? PlainText, string? DeletedAt)>(
            """
            SELECT e.id, c.markdown, c.plain_text, e.deleted_at
            FROM elements e LEFT JOIN page_contents c ON c.page_id = e.id
            WHERE e.kind = 'page'
            ORDER BY e.id
            """).ToList();
        var states = connection.Query<(long Id, string? DeletedAt)>("SELECT id, deleted_at FROM elements")
            .ToDictionary(e => e.Id, e => e.DeletedAt is not null);
        var tree = project.Repository.LoadTree(connection);
        var outOfSync = new List<(ElementId Id, string Markdown, MarkdownAnalysis Analysis)>();
        foreach (var page in pages)
        {
            var id = ElementId.FromInt64(page.Id);
            if (page.Markdown is null)
            {
                findings.Add(new(DoctorCodes.PageContentMissing, DoctorSeverity.Error, "The page has no content row.", id));
                continue;
            }

            var analysis = ErableMarkdown.Analyze(page.Markdown);
            var stored = project.Repository.GetOutgoingReferences(id, connection);
            var expected = analysis.References.Select(r => new StoredReference(id, r.Id, r.Anchor, r.IsEmbed, r.Start));
            if (page.PlainText != analysis.PlainText || !stored.SequenceEqual(expected))
            {
                outOfSync.Add((id, page.Markdown, analysis));
            }

            if (page.DeletedAt is not null || !tree.Contains(id))
            {
                continue;
            }

            foreach (var target in analysis.References.Select(r => r.Id).Distinct())
            {
                if (!states.TryGetValue(target.ToInt64(), out var deleted))
                {
                    findings.Add(new(DoctorCodes.ReferenceToUnknown, DoctorSeverity.Info, $"References unknown element {target}.", id));
                }
                else if (deleted || !tree.Contains(target))
                {
                    findings.Add(new(DoctorCodes.ReferenceToDeleted, DoctorSeverity.Info, $"References deleted element {target}.", id));
                }
            }
        }

        if (fix && outOfSync.Count > 0)
        {
            project.Database.WriteAsync((c, t) =>
            {
                foreach (var (id, markdown, analysis) in outOfSync)
                {
                    ProjectRepository.UpsertPageContent(c, t, id, markdown, analysis.PlainText);
                    ProjectRepository.ReplaceReferences(c, t, id,
                        analysis.References.Select(r => new StoredReference(id, r.Id, r.Anchor, r.IsEmbed, r.Start)));
                }

                return 0;
            }).GetAwaiter().GetResult();
        }

        findings.AddRange(outOfSync.Select(p => new DoctorFinding(
            DoctorCodes.IndexOutOfSync, DoctorSeverity.Warning, "The search text or reference index differs from the Markdown.", p.Id,
            Fixable: true, Fixed: fix)));
    }

    /// <summary>
    /// Categories, definitions and values (schema v2): rows of structure elements, inheritance cycles, values that no
    /// longer match their definition, and the derived tables (resolved values, validation issues), rebuilt by fix.
    /// </summary>
    private static void CheckStructure(OpenedProject project, bool fix, List<DoctorFinding> findings)
    {
        using var connection = project.Database.OpenConnection();
        foreach (var (id, kind) in connection.Query<(long Id, string Kind)>(
            """
            SELECT e.id, e.kind FROM elements e
            WHERE (e.kind = 'category' AND e.id NOT IN (SELECT id FROM categories))
               OR (e.kind = 'propertyDefinition' AND e.id NOT IN (SELECT id FROM property_defs))
               OR (e.kind = 'linkType' AND e.id NOT IN (SELECT id FROM link_types))
            ORDER BY e.id
            """))
        {
            findings.Add(new(DoctorCodes.StructureRowMissing, DoctorSeverity.Error, $"The {kind} has no row of its own (description, definition…).", ElementId.FromInt64(id)));
        }

        foreach (var (id, source, target, type) in connection.Query<(long Id, long Source, long Target, long Type)>(
            """
            SELECT l.id, l.source_id, l.target_id, l.link_type_id FROM links l
            WHERE l.source_id NOT IN (SELECT id FROM elements WHERE kind = 'page')
               OR l.target_id NOT IN (SELECT id FROM elements WHERE kind = 'page')
               OR l.link_type_id NOT IN (SELECT id FROM elements WHERE kind = 'linkType')
            ORDER BY l.id
            """))
        {
            findings.Add(new(DoctorCodes.LinkDangling, DoctorSeverity.Error,
                $"Link {ElementId.FromInt64(id)} refers to a missing page or link type ({ElementId.FromInt64(source)} → {ElementId.FromInt64(target)}, type {ElementId.FromInt64(type)}).",
                ElementId.FromInt64(source)));
        }

        var state = StructureState.Load(connection, null);
        foreach (var id in state.Graph.FindCycles())
        {
            findings.Add(new(DoctorCodes.CategoryCycle, DoctorSeverity.Error, "The category is part of an inheritance cycle.", id));
        }

        var elements = connection.Query<long>("SELECT id FROM elements").ToHashSet();
        var definitions = StructureRepository.LoadDefinitions(connection).ToDictionary(d => d.Id);
        foreach (var group in StructureRepository.LoadValueRows(connection).GroupBy(r => (r.OwnerId, r.PropertyId)))
        {
            var property = ElementId.FromInt64(group.Key.PropertyId);
            if (!definitions.TryGetValue(property, out var definition))
            {
                if (!elements.Contains(group.Key.PropertyId))
                {
                    findings.Add(new(DoctorCodes.ValueUnknownProperty, DoctorSeverity.Info, $"Value of unknown property {property}.", ElementId.FromInt64(group.Key.OwnerId)));
                }
            }
            else if (ValueStorage.Read([.. group], definition.Type) is null)
            {
                findings.Add(new(DoctorCodes.ValueInvalid, DoctorSeverity.Error,
                    $"The value of property {property} does not match its type {definition.Type}.", ElementId.FromInt64(group.Key.OwnerId)));
            }
        }

        var (resolved, issues) = state.ComputeDerived(null, null);
        if (!resolved.SequenceEqual(StructureRepository.LoadResolvedRows(connection)) || !issues.SequenceEqual(StructureRepository.LoadIssueRows(connection)))
        {
            if (fix)
            {
                project.Database.WriteAsync((c, t) =>
                {
                    StructureState.Load(c, null).WriteDerived(c, t, null, null);
                    return 0;
                }).GetAwaiter().GetResult();
            }

            findings.Add(new(DoctorCodes.DerivedOutOfSync, DoctorSeverity.Warning,
                "The resolved values or validation issues differ from the categories and values.", Fixable: true, Fixed: fix));
        }
    }

    /// <summary>The full-text index must hold exactly what <c>search_source</c> defines; fix rebuilds it.</summary>
    private static void CheckSearch(OpenedProject project, bool fix, List<DoctorFinding> findings)
    {
        using var connection = project.Database.OpenConnection();
        var damaged = false;
        try
        {
            connection.Execute("INSERT INTO search_fts (search_fts) VALUES ('integrity-check');");
        }
        catch (SqliteException)
        {
            damaged = true;
        }

        var expected = connection.Query<(long Id, string Name, string Body)>("SELECT id, name, body FROM search_source ORDER BY id").ToList();
        var stored = damaged
            ? []
            : connection.Query<(long Id, string Name, string Body)>("SELECT rowid, name, plain_text FROM search_fts ORDER BY rowid").ToList();
        if (!damaged && expected.SequenceEqual(stored))
        {
            return;
        }

        if (fix)
        {
            project.Database.WriteAsync((c, t) =>
            {
                c.Execute("DELETE FROM search_fts;", transaction: t);
                c.Execute("INSERT INTO search_fts (rowid, name, plain_text) SELECT id, name, body FROM search_source;", transaction: t);
                return 0;
            }).GetAwaiter().GetResult();
        }

        findings.Add(new(DoctorCodes.SearchOutOfSync, DoctorSeverity.Warning,
            damaged ? "The full-text index is damaged." : "The full-text index differs from the names and texts of the elements.",
            Fixable: true, Fixed: fix));
    }

    private static void CheckBlobs(OpenedProject project, bool fix, bool closed, List<DoctorFinding> findings)
    {
        using var connection = project.Database.OpenConnection();
        var blobs = project.Blobs;
        foreach (var (id, sha, size) in connection.Query<(long Id, string Sha256, long Size)>("SELECT id, sha256, size FROM attachments ORDER BY id"))
        {
            var elementId = ElementId.FromInt64(id);
            if (!DiskBlobStore.IsValidHash(sha) || !blobs.Exists(sha))
            {
                findings.Add(new(DoctorCodes.BlobMissing, DoctorSeverity.Error, "The file of the attachment is missing.", elementId, sha));
            }
            else if (!HasContent(blobs, sha, size))
            {
                findings.Add(new(DoctorCodes.BlobCorrupted, DoctorSeverity.Error, "The file of the attachment does not match its SHA-256 or size.", elementId, sha));
            }
        }

        foreach (var (id, sha) in connection.Query<(long Id, string Sha256)>(
            "SELECT DISTINCT element_id, blob_sha256 FROM revisions WHERE blob_sha256 IS NOT NULL AND blob_sha256 NOT IN (SELECT sha256 FROM attachments) ORDER BY element_id"))
        {
            if (!blobs.Exists(sha))
            {
                findings.Add(new(DoctorCodes.RevisionBlobMissing, DoctorSeverity.Warning,
                    "A revision of the attachment refers to a file that no longer exists (its history cannot be restored).", ElementId.FromInt64(id), sha));
            }
        }

        var referenced = project.Repository.GetReferencedBlobs(connection);
        foreach (var sha in blobs.List().Where(s => !referenced.Contains(s) && !blobs.IsLeased(s)).Order(StringComparer.Ordinal))
        {
            if (fix)
            {
                blobs.Delete(sha);
            }

            findings.Add(new(DoctorCodes.BlobOrphan, DoctorSeverity.Warning, "No attachment or revision uses this file.", Blob: sha, Fixable: true, Fixed: fix));
        }

        // In a running backend, a temporary file may be an upload in progress: only a closed project is checked.
        if (closed)
        {
            foreach (var file in blobs.ListTemporaryFiles())
            {
                if (fix)
                {
                    File.Delete(file);
                }

                findings.Add(new(DoctorCodes.BlobTemporary, DoctorSeverity.Warning, $"Leftover of an interrupted upload: {Path.GetFileName(file)}.",
                    Fixable: true, Fixed: fix));
            }
        }
    }

    private static bool HasContent(DiskBlobStore blobs, string sha, long size)
    {
        using var stream = blobs.OpenRead(sha);
        if (stream is null || stream.Length != size)
        {
            return false;
        }

        return Convert.ToHexStringLower(SHA256.HashData(stream)) == sha;
    }
}
