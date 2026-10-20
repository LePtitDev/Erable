using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core;
using Erable.Core.Projects;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;
using Erable.Storage;
using Erable.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

public sealed class ProjectsApiTests
{
    [Fact]
    public async Task CreateProject_ValidRequest_OpensItAndListsItFirstInRecents()
    {
        await using var backend = await TestBackend.StartAsync();

        var project = await backend.CreateProjectAsync("Atlas", "fr");

        project.Name.ShouldBe("Atlas");
        project.ContentLanguage.ShouldBe("fr");
        project.FormatVersion.ShouldBe(ProjectFormat.CurrentVersion);
        var recents = await (await backend.Client.GetAsync(new Uri("/api/v1/me/recent-projects", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<RecentProjectItem[]>();
        recents[0].ProjectId.ShouldBe(project.Id);
        recents[0].Status.ShouldBe(FolderStatus.Project);
    }

    [Fact]
    public async Task CreateProject_UnsupportedLanguage_Returns422WithCode()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(dir.Combine("p"), "Atlas", "tlh"));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await response.ProblemCodeAsync()).ShouldBe(StorageErrors.InvalidLanguage);
    }

    [Fact]
    public async Task CreateProject_WhereProjectExists_Returns409()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects", new CreateProjectRequest(project.Folder, "Encore", "fr"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe(StorageErrors.AlreadyExists);
    }

    [Fact]
    public async Task OpenProject_FolderWithoutManifest_Returns422AndWritesNothing()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();
        File.WriteAllText(dir.Combine("notes.txt"), "rien");
        var before = FolderFingerprint.Of(dir.Path);

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(dir.Path));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).ShouldBe(StorageErrors.NotAProject);
        FolderFingerprint.Of(dir.Path).ShouldBe(before);
    }

    [Fact]
    public async Task OpenProject_NewerFormat_Returns422AndLeavesFilesUnchanged()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();
        ProjectStore.Create(dir.Path, "Futur", "fr", TimeProvider.System).Value.Dispose();
        var manifest = JsonNode.Parse(File.ReadAllText(dir.Combine("erable.json")))!;
        manifest["formatVersion"] = ProjectFormat.CurrentVersion + 1;
        File.WriteAllText(dir.Combine("erable.json"), manifest.ToJsonString());
        var before = FolderFingerprint.Of(dir.Path);

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(dir.Path));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ProblemCodeAsync()).ShouldBe(StorageErrors.FormatTooNew);
        FolderFingerprint.Of(dir.Path).ShouldBe(before);
    }

    [Fact]
    public async Task OpenProject_AlreadyOpenHere_ReturnsSameProject()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(project.Folder));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsync<ProjectSummary>()).Id.ShouldBe(project.Id);
    }

    [Fact]
    public async Task OpenProject_LockedByAnotherInstance_Returns409()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();
        using var other = ProjectStore.Create(dir.Path, "Ailleurs", "fr", TimeProvider.System).Value;

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(dir.Path));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ProblemCodeAsync()).ShouldBe(StorageErrors.Locked);
    }

    [Fact]
    public async Task InspectFolder_EmptyFolder_IsNotAProject()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();

        using var response = await backend.Client.PostJsonAsync("/api/v1/projects/inspect", new InspectFolderRequest(dir.Path));

        (await response.ReadAsync<FolderInspection>()).Status.ShouldBe(FolderStatus.NotAProject);
    }

    [Fact]
    public async Task GetProject_NotOpen_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri($"/api/v1/projects/{ElementId.NewRandom()}", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ProblemCodeAsync()).ShouldBe(Problems.ProjectNotFound);
    }

    [Fact]
    public async Task GetProject_InvalidId_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.GetAsync(new Uri("/api/v1/projects/12", UriKind.Relative), TestContext.Current.CancellationToken);

        // A text that is no ID names no project the caller belongs to: same answer as any project they cannot see.
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RecentProjects_DeletedFolder_IsMissingAndRemovable()
    {
        await using var backend = await TestBackend.StartAsync();
        using var dir = TempDirectory.Create();
        var folder = dir.Combine("ephemere");
        ProjectStore.Create(folder, "Éphémère", "fr", TimeProvider.System).Value.Dispose();
        using (var open = await backend.Client.PostJsonAsync("/api/v1/projects/open", new OpenProjectRequest(folder)))
        {
            var summary = await open.ReadAsync<ProjectSummary>();
            backend.Services.GetRequiredService<Projects.ProjectWorkspace>().Close(summary.Id).ShouldBeTrue();
        }

        Directory.Delete(folder, recursive: true);
        var recents = await (await backend.Client.GetAsync(new Uri("/api/v1/me/recent-projects", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<RecentProjectItem[]>();
        recents.Single().Status.ShouldBe(FolderStatus.Missing);

        using var removed = await backend.Client.DeleteAsync(new Uri($"/api/v1/me/recent-projects/{recents[0].ProjectId}", UriKind.Relative), TestContext.Current.CancellationToken);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Directory.Exists(folder).ShouldBeFalse();
        (await (await backend.Client.GetAsync(new Uri("/api/v1/me/recent-projects", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<RecentProjectItem[]>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordLastElement_KnownProject_IsReturnedWithRecents()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = ElementId.NewRandom();

        using var response = await backend.Client.PutAsJsonAsync(
            $"/api/v1/me/recent-projects/{project.Id}/last-element", new RecordLastElementRequest(page), ApiJson.Options, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var recents = await (await backend.Client.GetAsync(new Uri("/api/v1/me/recent-projects", UriKind.Relative), TestContext.Current.CancellationToken))
            .ReadAsync<RecentProjectItem[]>();
        recents.Single().LastElementId.ShouldBe(page);
    }
}
