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

    /// <summary>
    /// Extensions the operating system runs rather than opens (programs, scripts, installers, shortcuts). « Open with
    /// the system application » refuses them: a click must never run a file that came from someone else.
    /// </summary>
    private static readonly HashSet<string> ExecutableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".app", ".appimage", ".appx", ".bat", ".cmd", ".com", ".command", ".cpl", ".deb", ".desktop", ".dll", ".dmg",
        ".exe", ".gadget", ".hta", ".inf", ".jar", ".js", ".jse", ".ksh", ".lnk", ".msc", ".msi", ".msix", ".msp",
        ".pif", ".pkg", ".ps1", ".psm1", ".py", ".rb", ".reg", ".rpm", ".run", ".scf", ".scr", ".sh", ".url", ".vb",
        ".vbe", ".vbs", ".ws", ".wsf", ".wsh",
    };

    /// <summary>Whether the operating system would run this file instead of opening it.</summary>
    public static bool IsExecutable(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        // Trailing dots and spaces are ignored by Windows: « setup.exe. » is « setup.exe ».
        return ExecutableExtensions.Contains(Path.GetExtension(fileName.TrimEnd('.', ' ')));
    }
}
