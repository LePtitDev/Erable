using System.IO.Compression;
using System.Text.Json;
using Erable.Core;
using Erable.Core.Elements;

namespace Erable.Storage;

/// <summary>
/// State of an element at one revision (F13.R1), stored as Brotli-compressed JSON in <c>revisions.snapshot</c>.
/// Categories and property values join it in M2.
/// </summary>
public sealed record RevisionSnapshot(
    ElementId Id, ElementKind Kind, ElementId ParentId, string Name, string? Markdown, AttachmentSnapshot? Attachment)
{
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
