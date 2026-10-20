using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Storage;

/// <summary>
/// State of an element at one revision (F13.R1), stored as Brotli-compressed JSON in <c>revisions.snapshot</c>.
/// From format v2 it also holds the categories and own values of a page, the fields of a category and the definition of
/// a property (absent from older snapshots).
/// </summary>
public sealed record RevisionSnapshot(
    ElementId Id, ElementKind Kind, ElementId ParentId, string Name, string? Markdown, AttachmentSnapshot? Attachment)
{
    /// <summary>Categories of a page, in their order.</summary>
    public IReadOnlyList<ElementId>? Categories { get; init; }

    /// <summary>Values defined by a page or a category: property ID → JSON value.</summary>
    public IReadOnlyDictionary<string, JsonNode?>? Values { get; init; }

    /// <summary>Description and parent of a category.</summary>
    public CategorySnapshot? Category { get; init; }

    /// <summary>Description and inverse label of a link type.</summary>
    public LinkTypeSnapshot? LinkType { get; init; }

    /// <summary>Definition of a property (key, type, default, rules, options).</summary>
    public JsonObject? Definition { get; init; }

    /// <summary>The revision this one restores (F13.R3); absent from every other revision.</summary>
    public long? RestoredFrom { get; init; }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public byte[] Serialize()
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest))
        {
            JsonSerializer.Serialize(brotli, this, Json);
        }

        return output.ToArray();
    }

    public static RevisionSnapshot Deserialize(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        using var brotli = new BrotliStream(new MemoryStream(data), CompressionMode.Decompress);
        return JsonSerializer.Deserialize<RevisionSnapshot>(brotli, Json)
            ?? throw new InvalidDataException("Empty revision snapshot.");
    }
}

/// <summary>Attachment metadata kept in its revisions (the blob stays alive while a revision references it).</summary>
public sealed record AttachmentSnapshot(string FileName, string Mime, long Size, string Sha256);

/// <summary>Stored fields of a category kept in its revisions.</summary>
public sealed record CategorySnapshot(string Description, ElementId? ParentCategoryId);

/// <summary>Stored fields of a link type kept in its revisions.</summary>
public sealed record LinkTypeSnapshot(string Description, string? InverseName);
