namespace Erable.Testing;

/// <summary>
/// A fresh folder per test, deleted on dispose (docs/testing.md §6: real SQLite in a temporary folder).
/// <code>using var dir = TempDirectory.Create(); var db = dir.Combine("erable.db");</code>
/// </summary>
public sealed class TempDirectory : IDisposable
{
    private TempDirectory(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempDirectory Create(string prefix = "erable-test-") =>
        new(Directory.CreateTempSubdirectory(prefix).FullName);

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        // SQLite (WAL) and antivirus scanners may hold files for a moment on Windows: retry briefly.
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }

                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(50 * attempt);
            }
            catch (UnauthorizedAccessException) when (attempt < 5)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }
}
