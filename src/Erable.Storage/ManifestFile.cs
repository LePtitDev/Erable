using System.Text.Json;
using System.Text.Json.Serialization;
using Erable.Core;
using Erable.Core.Projects;

namespace Erable.Storage;

/// <summary>Reads and writes <c>erable.json</c>.</summary>
public static class ManifestFile
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        // Same bytes on every OS (the default follows Environment.NewLine).
        NewLine = "\n",
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string PathIn(string folder) => Path.Combine(folder, ProjectFiles.Manifest);

    public static bool Exists(string folder) => File.Exists(PathIn(folder));

    /// <summary>Reads the manifest without writing anything (F01.R1).</summary>
    public static Result<ProjectManifest> Read(string folder)
    {
        var path = PathIn(folder);
        if (!File.Exists(path))
        {
            return new DomainError(StorageErrors.NotAProject, $"'{folder}' contains no {ProjectFiles.Manifest}.");
        }

        try
        {
            var manifest = JsonSerializer.Deserialize<ProjectManifest>(File.ReadAllText(path), Json);
            return manifest is { FormatVersion: > 0, ProjectId.IsNone: false, Name.Length: > 0, ContentLanguage.Length: > 0 }
                ? manifest
                : new DomainError(StorageErrors.InvalidManifest, $"{ProjectFiles.Manifest} is incomplete.");
        }
        catch (JsonException ex)
        {
            return new DomainError(StorageErrors.InvalidManifest, $"{ProjectFiles.Manifest} is not valid: {ex.Message}");
        }
    }

    /// <summary>Atomic write: temporary file then rename, so that a crash never leaves a truncated manifest.</summary>
    public static void Write(string folder, ProjectManifest manifest)
    {
        var path = PathIn(folder);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(manifest, Json) + "\n");
        File.Move(temporary, path, overwrite: true);
    }
}

/// <summary>Business error codes of project storage (see <see cref="ErrorCodes"/>).</summary>
public static class StorageErrors
{
    /// <summary>The folder has no <c>erable.json</c> (F01.R1).</summary>
    public const string NotAProject = "project.notAProject";

    public const string InvalidManifest = "project.manifestInvalid";

    /// <summary>The project was written by a newer application (F01.R3).</summary>
    public const string FormatTooNew = "project.formatTooNew";

    /// <summary>Another instance has the project open (F16.R2).</summary>
    public const string Locked = "project.locked";

    /// <summary>Creating a project where one already exists.</summary>
    public const string AlreadyExists = "project.alreadyExists";

    public const string InvalidLanguage = "project.languageInvalid";

    /// <summary>The folder cannot be read or written.</summary>
    public const string Inaccessible = "project.inaccessible";

    /// <summary>
    /// An older project needs its format migrated, which only happens once the user confirmed (F01.R3);
    /// <c>fromVersion</c> and <c>toVersion</c> in the extensions.
    /// </summary>
    public const string MigrationRequired = "project.migration.required";

    /// <summary>
    /// The migration failed and the project is back to its original state (<c>backup</c> in the extensions when the
    /// safety copy was kept because the restoration itself failed).
    /// </summary>
    public const string MigrationFailed = "project.migration.failed";
}
