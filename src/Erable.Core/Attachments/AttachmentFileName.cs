using Erable.Core.Elements;

namespace Erable.Core.Attachments;

/// <summary>Name of an uploaded file, which becomes the name of its attachment element.</summary>
public static class AttachmentFileName
{
    /// <summary>
    /// Keeps the last path segment (browsers and tools may send <c>C:\fakepath\x.png</c> or <c>dir/x.png</c>), then applies
    /// the element name rules.
    /// </summary>
    public static Result<string> Normalize(string? fileName)
    {
        var name = fileName ?? string.Empty;
        var slash = name.LastIndexOfAny(['/', '\\']);
        if (slash >= 0)
        {
            name = name[(slash + 1)..];
        }

        return ElementName.Normalize(name);
    }
}
