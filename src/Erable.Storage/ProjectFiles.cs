using System.Globalization;
using Erable.Core.Elements;

namespace Erable.Storage;

/// <summary>Names of the files of a project folder (docs/architecture.md §7.1).</summary>
public static class ProjectFiles
{
    public const string Manifest = "erable.json";
    public const string Database = "erable.db";
    public const string Blobs = "blobs";

    /// <summary>Single-instance lock (F16.R2, decision P-04); ignored by <c>erable doctor</c>.</summary>
    public const string Lock = "erable.lock";

    /// <summary>Prefix of the database backups made before a migration: <c>erable.db.bak-20261013-080000</c>.</summary>
    public const string BackupPrefix = "erable.db.bak-";
}

/// <summary>Storage conventions shared by every table.</summary>
internal static class StorageFormat
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>UTC ISO 8601 with milliseconds: sortable as text and readable in any SQLite tool.</summary>
    public static string Timestamp(DateTimeOffset value) =>
        value.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);

    public static DateTimeOffset ParseTimestamp(string value) =>
        DateTimeOffset.ParseExact(value, TimestampFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static DateTimeOffset? ParseTimestampOrNull(string? value) => value is null ? null : ParseTimestamp(value);

    public static string Kind(ElementKind kind) => kind switch
    {
        ElementKind.Root => "root",
        ElementKind.Folder => "folder",
        ElementKind.Page => "page",
        ElementKind.Category => "category",
        ElementKind.PropertyDefinition => "propertyDefinition",
        ElementKind.LinkType => "linkType",
        ElementKind.Attachment => "attachment",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static ElementKind ParseKind(string value) => value switch
    {
        "root" => ElementKind.Root,
        "folder" => ElementKind.Folder,
        "page" => ElementKind.Page,
        "category" => ElementKind.Category,
        "propertyDefinition" => ElementKind.PropertyDefinition,
        "linkType" => ElementKind.LinkType,
        "attachment" => ElementKind.Attachment,
        _ => throw new InvalidDataException($"Unknown element kind '{value}' in the database."),
    };
}
