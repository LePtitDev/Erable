using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing;

namespace Erable.Server.Tests;

/// <summary>Opening a project of an older format over HTTP: only after the user confirmed (F01.R3).</summary>
public sealed class ProjectMigrationApiTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(TestBackend Backend, string Folder)> StartWithLegacyAsync(int version)
    {
        var backend = await TestBackend.StartAsync();
        var folder = Path.Combine(backend.Options.DataDirectory!, "projects", "legacy");
        FolderFingerprint.Copy(RepositoryPaths.Combine("spec", "projects", "legacy", $"v{version}"), folder);
        return (backend, folder);
    }

    [Fact]
    public async Task Open_OlderProjectWithoutConfirmation_Returns409AndWritesNothing()
    {
        var (backend, folder) = await StartWithLegacyAsync(1);
        await using var _ = backend;
        var before = FolderFingerprint.Of(folder);

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = (await response.Content.ReadFromJsonAsync<JsonObject>(Ct))!;
        problem["code"]!.GetValue<string>().ShouldBe(StorageErrors.MigrationRequired);
        problem["fromVersion"]!.GetValue<int>().ShouldBe(1);
        problem["toVersion"]!.GetValue<int>().ShouldBe(ProjectFormat.CurrentVersion);
        FolderFingerprint.Of(folder).ShouldBe(before);
    }

    [Fact]
    public async Task Open_OlderProjectConfirmed_MigratesAfterABackup()
    {
        // E2E-01-05
        var (backend, folder) = await StartWithLegacyAsync(1);
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder, ConfirmMigration: true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<ProjectSummary>()).FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
        Directory.GetFiles(folder, ProjectFiles.BackupPrefix + "*").Length.ShouldBe(1);
    }

    [Fact]
    public async Task Open_AlreadyMigratedProject_NeedsNoConfirmation()
    {
        var (backend, folder) = await StartWithLegacyAsync(ProjectFormat.CurrentVersion);
        await using var _ = backend;

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Directory.GetFiles(folder, ProjectFiles.BackupPrefix + "*").ShouldBeEmpty();
    }
}
