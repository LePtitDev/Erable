namespace Erable.Server.Tests.Infrastructure;

internal static class RepositoryPaths
{
    /// <summary>Repository root, found by walking up from the test binaries to <c>Erable.sln</c>.</summary>
    public static string Root { get; } = FindRoot();

    public static string Combine(params string[] parts) => Path.Combine([Root, .. parts]);

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Erable.sln")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (Erable.sln) not found.");
    }
}
