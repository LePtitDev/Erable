using Erable.Core;

namespace Erable.Core.Tests;

public sealed class ArchitectureTests
{
    // Core must stay pure: no IO, database, HTTP or hosting dependency (CLAUDE.md §6.2).
    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.Data",
        "System.Data",
        "Dapper",
        "Microsoft.AspNetCore",
        "Microsoft.Extensions.Hosting",
        "System.Net.Http",
        "Photino",
        "Erable.Storage",
        "Erable.Server",
        "Erable.Desktop",
    ];

    [Fact]
    public void CoreAssembly_ReferencedAssemblies_ContainNoInfrastructure()
    {
        var referenced = CoreAssembly.Assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        referenced
            .Where(name => ForbiddenPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ShouldBeEmpty();
    }
}
