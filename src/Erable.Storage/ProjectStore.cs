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
        _lock = projectLock;
        BackupPath = backupPath;
    }

    /// <summary>Absolute path of the project folder.</summary>
    public string Folder { get; }

    public ProjectManifest Manifest { get; }

    public ProjectDatabase Database { get; }

    public ProjectRepository Repository { get; }

    public string BlobsDirectory { get; }

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
    /// project is migrated after a backup of its database (<c>erable.db.bak-…</c>); the confirmation asked to the
    /// user arrives with M2-09.
    /// </summary>
    public static Result<OpenedProject> Open(string folder, TimeProvider clock)
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

            string? backup = null;
            if (schemaVersion < ProjectFormat.CurrentVersion)
            {
                backup = database.Backup(clock.GetUtcNow());
                using (var connection = database.OpenConnection())
                {
                    Migrations.Apply(connection, schemaVersion);
                }

                manifest = manifest with { FormatVersion = ProjectFormat.CurrentVersion };
                ManifestFile.Write(folder, manifest);
            }

            Directory.CreateDirectory(Path.Combine(folder, ProjectFiles.Blobs));
            return new OpenedProject(folder, manifest, database, acquired.Value, backup);
        }
        catch
        {
            database?.Dispose();
            acquired.Value.Dispose();
            throw;
        }
    }

    private static DomainError TooNew(int version) => new(
        StorageErrors.FormatTooNew,
        $"The project uses format {version}; this application reads up to {ProjectFormat.CurrentVersion}. Update the application.");
}
