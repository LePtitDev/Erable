using System.Security.Cryptography;

namespace Erable.Testing;

/// <summary>Relative path and SHA-256 of every file of a folder: proves that an operation wrote nothing.</summary>
public static class FolderFingerprint
{
    public static IReadOnlyDictionary<string, string> Of(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToDictionary(
                f => Path.GetRelativePath(folder, f).Replace('\\', '/'),
                f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))),
                StringComparer.Ordinal);

    /// <summary>Copies a folder recursively (fixtures are copied before being opened: opening may write).</summary>
    public static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
