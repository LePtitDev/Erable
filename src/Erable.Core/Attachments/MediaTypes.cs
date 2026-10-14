using System.Text;

namespace Erable.Core.Attachments;

/// <summary>How the SPA presents an attachment (F06.R2).</summary>
public enum AttachmentDisplay
{
    Image,
    Video,
    Audio,
    Pdf,

    /// <summary>A downloadable card: everything else, SVG and HTML included (never rendered inline, docs/architecture.md §9).</summary>
    File,
}

/// <summary>
/// Media type of an uploaded file, detected from its first bytes first and its extension second, and the safety rules
/// that follow (F06.R2): a user file is never served with a type that lets a browser run script.
/// </summary>
public static class MediaTypes
{
    public const string OctetStream = "application/octet-stream";

    /// <summary>Bytes needed by <see cref="Detect"/>.</summary>
    public const int SniffLength = 512;

    private static readonly Dictionary<string, string> ByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".avif"] = "image/avif",
        [".svg"] = "image/svg+xml",
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".webm"] = "video/webm",
        [".ogv"] = "video/ogg",
        [".mp3"] = "audio/mpeg",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".wav"] = "audio/wav",
        [".m4a"] = "audio/mp4",
        [".flac"] = "audio/flac",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".json"] = "application/json",
        [".csv"] = "text/csv",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".xhtml"] = "application/xhtml+xml",
        [".xml"] = "application/xml",
    };

    /// <summary>Types rendered inline by the SPA; anything else is a download.</summary>
    private static readonly Dictionary<string, AttachmentDisplay> Inline = new(StringComparer.Ordinal)
    {
        ["image/png"] = AttachmentDisplay.Image,
        ["image/jpeg"] = AttachmentDisplay.Image,
        ["image/gif"] = AttachmentDisplay.Image,
        ["image/webp"] = AttachmentDisplay.Image,
        ["image/avif"] = AttachmentDisplay.Image,
        ["video/mp4"] = AttachmentDisplay.Video,
        ["video/webm"] = AttachmentDisplay.Video,
        ["video/ogg"] = AttachmentDisplay.Video,
        ["audio/mpeg"] = AttachmentDisplay.Audio,
        ["audio/ogg"] = AttachmentDisplay.Audio,
        ["audio/wav"] = AttachmentDisplay.Audio,
        ["audio/mp4"] = AttachmentDisplay.Audio,
        ["audio/flac"] = AttachmentDisplay.Audio,
        ["application/pdf"] = AttachmentDisplay.Pdf,
    };

    /// <summary>
    /// Media type of a file: a binary signature wins over the extension; markup (HTML, SVG, XML) found in the content
    /// is reported as such whatever the extension, so that it is never mistaken for a harmless type.
    /// </summary>
    public static string Detect(ReadOnlySpan<byte> head, string fileName)
    {
        if (DetectSignature(head) is { } signed)
        {
            return signed;
        }

        if (DetectMarkup(head) is { } markup)
        {
            return markup;
        }

        var extension = Path.GetExtension(fileName ?? string.Empty);
        return ByExtension.TryGetValue(extension, out var byExtension) ? byExtension : OctetStream;
    }

    public static AttachmentDisplay DisplayOf(string mediaType) =>
        Inline.TryGetValue(mediaType ?? string.Empty, out var display) ? display : AttachmentDisplay.File;

    /// <summary>Whether the file may be shown inline (<c>Content-Disposition: inline</c>); false forces a download.</summary>
    public static bool IsInlineSafe(string mediaType) => DisplayOf(mediaType) != AttachmentDisplay.File;

    private static string? DetectSignature(ReadOnlySpan<byte> h)
    {
        // A byte array, not a u8 literal: "\x89"u8 is the UTF-8 encoding of U+0089, two bytes.
        if (h.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (h.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (h.StartsWith("GIF87a"u8) || h.StartsWith("GIF89a"u8))
        {
            return "image/gif";
        }

        if (h.Length >= 12 && h.StartsWith("RIFF"u8))
        {
            var kind = h.Slice(8, 4);
            if (kind.SequenceEqual("WEBP"u8))
            {
                return "image/webp";
            }

            if (kind.SequenceEqual("WAVE"u8))
            {
                return "audio/wav";
            }
        }

        if (h.Length >= 12 && h.Slice(4, 4).SequenceEqual("ftyp"u8))
        {
            var brand = h.Slice(8, 4);
            return brand.SequenceEqual("avif"u8) || brand.SequenceEqual("avis"u8) ? "image/avif"
                : brand.StartsWith("M4A"u8) ? "audio/mp4"
                : "video/mp4";
        }

        if (h.StartsWith((ReadOnlySpan<byte>)[0x1A, 0x45, 0xDF, 0xA3]))
        {
            return "video/webm";
        }

        if (h.StartsWith("OggS"u8))
        {
            return "audio/ogg";
        }

        if (h.StartsWith("fLaC"u8))
        {
            return "audio/flac";
        }

        if (h.StartsWith("ID3"u8) || (h.Length >= 2 && h[0] == 0xFF && (h[1] & 0xE0) == 0xE0))
        {
            return "audio/mpeg";
        }

        if (h.StartsWith("%PDF-"u8))
        {
            return "application/pdf";
        }

        if (h.StartsWith("PK\x03\x04"u8))
        {
            return "application/zip";
        }

        return null;
    }

    /// <summary>HTML, SVG or XML at the start of a text file (after a BOM and whitespace), case-insensitive.</summary>
    private static string? DetectMarkup(ReadOnlySpan<byte> head)
    {
        var text = Encoding.UTF8.GetString(head.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]) ? head[3..] : head).TrimStart().ToLowerInvariant();
        if (text.Contains("<svg", StringComparison.Ordinal))
        {
            return "image/svg+xml";
        }

        if (text.StartsWith("<!doctype html", StringComparison.Ordinal) || text.StartsWith("<html", StringComparison.Ordinal)
            || text.Contains("<script", StringComparison.Ordinal) || text.Contains("<body", StringComparison.Ordinal)
            || text.Contains("<iframe", StringComparison.Ordinal))
        {
            return "text/html";
        }

        return text.StartsWith("<?xml", StringComparison.Ordinal) ? "application/xml" : null;
    }
}
