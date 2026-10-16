using Erable.Core;
using Erable.Core.Elements;
using Erable.Core.Projects;
using Erable.Core.Tree;

namespace Erable.Storage;

/// <summary>An open project: holds the single-instance lock until disposed.</summary>
public sealed class OpenedProject : IDisposable
{
    private readonly ProjectLock _lock;

    internal OpenedProject(string folder, ProjectManifest manifest, ProjectDatabase database, ProjectLock projectLock, string? backupPath)
    {
        Folder = folder;
        Manifest = manifest;
        Database = database;
        Repository = new ProjectRepository(database);
        BlobsDirectory = Path.Combine(folder, ProjectFiles.Blobs);
        Blobs = new DiskBlobStore(BlobsDirectory);
        _lock = projectLock;
        BackupPath = backupPath;
    }

    /// <summary>Absolute path of the project folder.</summary>
    public string Folder { get; }

    public ProjectManifest Manifest { get; }

    public ProjectDatabase Database { get; }

    public ProjectRepository Repository { get; }

    public string BlobsDirectory { get; }

    public DiskBlobStore Blobs { get; }

    /// <summary>Backup made before a migration at this opening, if any.</summary>
    public string? BackupPath { get; }

    public void Dispose()
    {
        Database.Dispose();
        _lock.Dispose();
    }
}

/// <summary>What can be said about a folder without writing anything (home screen, F01.R1, F01.R3).</summary>
public enum FolderStatus
{
    /// <summary>The folder does not exist.</summary>
    Missing,

    /// <summary>The folder exists but has no manifest: creating a project there is possible.</summary>
    NotAProject,

    /// <summary>An Erable project this application can open (possibly after a migration).</summary>
    Project,

    /// <summary>An Erable project written by a newer application (F01.R3).</summary>
    TooNew,

    /// <summary>A manifest that cannot be read.</summary>
    Invalid,
}

/// <summary>Creates, inspects and opens projects (docs/architecture.md §7.1).</summary>
public static class ProjectStore
{
    /// <summary>Reads only: never creates nor modifies a file.</summary>
    public static (FolderStatus Status, ProjectManifest? Manifest) Inspect(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return (FolderStatus.Missing, null);
        }

        if (!ManifestFile.Exists(folder))
        {
            return (FolderStatus.NotAProject, null);
        }

        var manifest = ManifestFile.Read(folder);
        if (!manifest.IsSuccess)
        {
            return (FolderStatus.Invalid, null);
        }

        return (manifest.Value.FormatVersion > ProjectFormat.CurrentVersion ? FolderStatus.TooNew : FolderStatus.Project, manifest.Value);
    }

    /// <summary>
    /// Creates a project in <paramref name="folder"/> (created if missing). The manifest is written last: an
    /// interrupted creation leaves a folder that is not (yet) a project, never a half-valid one.
    /// </summary>
    public static Result<OpenedProject> Create(string folder, string name, string contentLanguage, TimeProvider clock, string author = KnownUsers.Local)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(clock);
        var normalizedName = ElementName.Normalize(name);
        if (!normalizedName.IsSuccess)
        {
            return normalizedName.Error;
        }

        if (!ContentLanguages.IsSupported(contentLanguage))
        {
            return new DomainError(StorageErrors.InvalidLanguage, $"Unsupported content language '{contentLanguage}'.");
        }

        folder = Path.GetFullPath(folder);
        if (ManifestFile.Exists(folder) || File.Exists(Path.Combine(folder, ProjectFiles.Database)))
        {
            return new DomainError(StorageErrors.AlreadyExists, $"'{folder}' already contains a project.");
        }

        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new DomainError(StorageErrors.Inaccessible, ex.Message);
        }

        var acquired = ProjectLock.Acquire(folder);
        if (!acquired.IsSuccess)
        {
            return acquired.Error;
        }

        try
        {
            var now = clock.GetUtcNow();
            var database = ProjectDatabase.Create(Path.Combine(folder, ProjectFiles.Database));
            database.Write((connection, transaction) =>
            {
                foreach (var node in ElementTree.CreateEmpty().Nodes)
                {
                    ProjectRepository.InsertElement(connection, transaction, new Element(
                        node.Id, node.Kind, node.ParentId, node.Name, node.SortOrder, 1, now, author, now, author, null));
                }

                return 0;
            });
            Directory.CreateDirectory(Path.Combine(folder, ProjectFiles.Blobs));

            var manifest = new ProjectManifest(
                ProjectFormat.CurrentVersion, ElementId.NewRandom(), normalizedName.Value, ContentLanguages.Normalize(contentLanguage), now);
            ManifestFile.Write(folder, manifest);
            return new OpenedProject(folder, manifest, database, acquired.Value, backupPath: null);
        }
        catch
        {
            acquired.Value.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a project. Nothing is written when the folder is not a project or is too new (F01.R1, F01.R3). An older
    /// project is migrated only when <paramref name="migrate"/> says the user confirmed (otherwise the answer is
    /// <see cref="StorageErrors.MigrationRequired"/>, nothing touched): a consistent backup of the database comes first
    /// (<c>erable.db.bak-…</c>), then the migrations, then the doctor; if anything fails the backup is put back and the
    /// project is exactly as it was (F01.R3, M2-09).
    /// </summary>
    /// <param name="onMigrationStep">Called with each schema version reached during a migration (test seam for failures).</param>
    public static Result<OpenedProject> Open(string folder, TimeProvider clock, bool migrate = true, Action<int>? onMigrationStep = null)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(clock);
        folder = Path.GetFullPath(folder);
        var read = ManifestFile.Read(folder);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var manifest = read.Value;
        if (manifest.FormatVersion > ProjectFormat.CurrentVersion)
        {
            return TooNew(manifest.FormatVersion);
        }

        var databasePath = Path.Combine(folder, ProjectFiles.Database);
        if (!File.Exists(databasePath))
        {
            return new DomainError(StorageErrors.InvalidManifest, $"{ProjectFiles.Database} is missing.");
        }

        var acquired = ProjectLock.Acquire(folder);
        if (!acquired.IsSuccess)
        {
            return acquired.Error;
        }

        ProjectDatabase? database = null;
        try
        {
            database = ProjectDatabase.Open(databasePath);
            var schemaVersion = database.ReadSchemaVersion();
            if (schemaVersion > ProjectFormat.CurrentVersion)
            {
                database.Dispose();
                acquired.Value.Dispose();
                return TooNew(schemaVersion);
            }

            if (schemaVersion < ProjectFormat.CurrentVersion)
            {
                if (!migrate)
                {
                    database.Dispose();
                    acquired.Value.Dispose();
                    return new DomainError(
                        StorageErrors.MigrationRequired,
                        $"The project uses format {schemaVersion}; it must be migrated to {ProjectFormat.CurrentVersion}.",
                        new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["fromVersion"] = schemaVersion,
                            ["toVersion"] = ProjectFormat.CurrentVersion,
                        });
                }

                var migrated = Migrate(folder, manifest, database, acquired.Value, schemaVersion, clock, onMigrationStep);
                if (!migrated.IsSuccess)
                {
                    acquired.Value.Dispose();
                    return migrated.Error;
                }

                Directory.CreateDirectory(Path.Combine(folder, ProjectFiles.Blobs));
                return migrated.Value;
            }

            Directory.CreateDirectory(Path.Combine(folder, ProjectFiles.Blobs));
            return new OpenedProject(folder, manifest, database, acquired.Value, null);
        }
        catch
        {
            database?.Dispose();
            acquired.Value.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Backup, migrations, manifest, then a check by the doctor. On any failure the database is closed and replaced by
    /// the backup, the manifest is put back, and the error says so; the lock stays with the caller.
    /// </summary>
    private static Result<OpenedProject> Migrate(
        string folder, ProjectManifest original, ProjectDatabase database, ProjectLock projectLock, int from, TimeProvider clock,
        Action<int>? onStep)
    {
        var backup = database.Backup(clock.GetUtcNow());
        var manifestWritten = false;
        try
        {
            using (var connection = database.OpenConnection())
            {
                Migrations.Apply(connection, from, onStep);
            }

            var migrated = original with { FormatVersion = ProjectFormat.CurrentVersion };
            ManifestFile.Write(folder, migrated);
            manifestWritten = true;

            var project = new OpenedProject(folder, migrated, database, projectLock, backup);
            if (ProjectDoctor.Diagnose(project, fix: false).Findings.FirstOrDefault(f => f.Severity == DoctorSeverity.Error) is { } problem)
            {
                throw new InvalidDataException($"The migrated project does not pass the check ({problem.Code}: {problem.Message}).");
            }

            return project;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            database.Dispose();
            var extensions = new Dictionary<string, object?>(StringComparer.Ordinal) { ["fromVersion"] = from };
            try
            {
                RestoreDatabase(Path.Combine(folder, ProjectFiles.Database), backup);
                if (manifestWritten)
                {
                    ManifestFile.Write(folder, original);
                }
            }
            catch (Exception restore) when (restore is IOException or UnauthorizedAccessException)
            {
                // The safety copy is the only intact version left: say where it is.
                extensions["backup"] = backup;
            }

            return new DomainError(
                StorageErrors.MigrationFailed, $"The migration failed and the project was restored: {ex.Message}", extensions);
        }
    }

    /// <summary>Puts the backup back as the database (leftovers of the write-ahead log removed), then deletes the backup.</summary>
    private static void RestoreDatabase(string databasePath, string backup)
    {
        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            File.Delete(databasePath + suffix);
        }

        File.Copy(backup, databasePath, overwrite: true);
        File.Delete(backup);
    }

    private static DomainError TooNew(int version) => new(
        StorageErrors.FormatTooNew,
        $"The project uses format {version}; this application reads up to {ProjectFormat.CurrentVersion}. Update the application.");
}
