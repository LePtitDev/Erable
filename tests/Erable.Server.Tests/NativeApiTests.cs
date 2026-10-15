using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Hosting;
using Erable.Server.Native;
using Erable.Server.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Erable.Server.Tests;

public sealed class NativeApiTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>What the desktop shell was asked to do.</summary>
    private sealed class FakeBridge : INativeBridge
    {
        public ConcurrentQueue<string> Calls { get; } = new();

        public string? Picked { get; set; } = @"C:\Wikis\Chroniques";

        public bool IsAvailable => true;

        public Task<string?> PickFolderAsync(string title, CancellationToken cancellationToken)
        {
            Calls.Enqueue($"pick:{title}");
            return Task.FromResult(Picked);
        }

        public Task OpenWithSystemAsync(string path, CancellationToken cancellationToken)
        {
            Calls.Enqueue($"open:{path}");
            return Task.CompletedTask;
        }

        public Task RevealInFileManagerAsync(string path, CancellationToken cancellationToken)
        {
            Calls.Enqueue($"reveal:{path}");
            return Task.CompletedTask;
        }

        public void SetWindowTitle(string title) => Calls.Enqueue($"title:{title}");

        public void BringToFront() => Calls.Enqueue("front");

        public void AllowForeground(int processId) => Calls.Enqueue("allow");

        public void CloseWindow() => Calls.Enqueue("close");
    }

    private static Task<TestBackend> StartDesktopAsync(FakeBridge bridge, string? profile = null) =>
        TestBackend.StartAsync(
            o => o with { Mode = HostMode.Desktop, DataDirectory = profile ?? o.DataDirectory },
            services: s => s.AddSingleton<INativeBridge>(bridge));

    [Fact]
    public async Task PickFolder_ServerMode_Returns404Unavailable()
    {
        await using var backend = await TestBackend.StartAsync();

        using var response = await backend.Client.PostAsJsonAsync("/api/v1/native/pick-folder", new PickFolderRequest("Ouvrir"), Ct);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        problem!["code"]!.GetValue<string>().ShouldBe(NativeEndpoints.Unavailable);
    }

    [Fact]
    public async Task PickFolder_Desktop_ReturnsTheChosenFolder()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);

        using var response = await backend.Client.PostAsJsonAsync("/api/v1/native/pick-folder", new PickFolderRequest("Ouvrir un dossier"), Ct);

        (await response.ReadAsync<PickedFolder>()).Path.ShouldBe(@"C:\Wikis\Chroniques");
        bridge.Calls.ShouldContain("pick:Ouvrir un dossier");
    }

    [Fact]
    public async Task PickFolder_Cancelled_ReturnsNull()
    {
        var bridge = new FakeBridge { Picked = null };
        await using var backend = await StartDesktopAsync(bridge);

        using var response = await backend.Client.PostAsJsonAsync("/api/v1/native/pick-folder", new PickFolderRequest("Ouvrir"), Ct);

        (await response.ReadAsync<PickedFolder>()).Path.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateWindow_Title_ReachesTheShell()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);

        using var response = await backend.Client.PutAsJsonAsync("/api/v1/native/window", new WindowState("Chroniques d'Aube — Erable", false), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        bridge.Calls.ShouldContain("title:Chroniques d'Aube — Erable");
    }

    [Fact]
    public async Task OpenAttachment_Image_OpensANamedCopyWithTheSameContent()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "carte.png", Png)).ReadAsync<AttachmentItem>();

        using var response = await backend.Client.PostAsync(
            new Uri($"/api/v1/projects/{project.Id}/attachments/{item.Id}/open", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var path = bridge.Calls.Single(c => c.StartsWith("open:", StringComparison.Ordinal))["open:".Length..];
        Path.GetFileName(path).ShouldBe("carte.png");
        (await File.ReadAllBytesAsync(path, Ct)).ShouldBe(Png);
    }

    [Fact]
    public async Task OpenAttachment_Executable_IsRefusedAndNeverRun()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "outil.exe", [0x4D, 0x5A, 0, 0])).ReadAsync<AttachmentItem>();

        using var response = await backend.Client.PostAsync(
            new Uri($"/api/v1/projects/{project.Id}/attachments/{item.Id}/open", UriKind.Relative), null, Ct);
        var problem = await response.Content.ReadFromJsonAsync<JsonObject>(Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        problem!["code"]!.GetValue<string>().ShouldBe(NativeEndpoints.ExecutableRefused);
        bridge.Calls.ShouldNotContain(c => c.StartsWith("open:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RevealAttachment_Desktop_RevealsTheNamedCopy()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "outil.exe", [0x4D, 0x5A])).ReadAsync<AttachmentItem>();

        using var response = await backend.Client.PostAsync(
            new Uri($"/api/v1/projects/{project.Id}/attachments/{item.Id}/reveal", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        bridge.Calls.ShouldContain(c => c.StartsWith("reveal:", StringComparison.Ordinal) && c.EndsWith("outil.exe", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Events_CloseWithUnsavedChanges_AsksTheSpaAndKeepsTheWindow()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);
        var window = backend.Services.GetRequiredService<WindowCoordinator>();
        using var state = await backend.Client.PutAsJsonAsync("/api/v1/native/window", new WindowState("Erable", true), Ct);
        // The headers only leave with the first event: the request completes after OnClosing.
        var pending = backend.Client.GetAsync(
            new Uri("/api/v1/native/events", UriKind.Relative), HttpCompletionOption.ResponseHeadersRead, Ct);

        await WaitUntilAsync(() => window.OnClosing());
        using var events = await pending;
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync(Ct));
        var line = await reader.ReadLineAsync(Ct);
        while (line is not null && !line.StartsWith("data:", StringComparison.Ordinal))
        {
            line = await reader.ReadLineAsync(Ct);
        }

        line.ShouldNotBeNull().ShouldContain("closeRequested");
        bridge.Calls.ShouldNotContain("close");
    }

    [Fact]
    public void OnClosing_NoUnsavedChanges_ClosesAtOnce()
    {
        var window = new WindowCoordinator(new FakeBridge());

        window.OnClosing().ShouldBeFalse();
    }

    [Fact]
    public void OnClosing_UnsavedChangesButNoSpaListening_Closes()
    {
        var window = new WindowCoordinator(new FakeBridge());
        window.SetUnsavedChanges(true);

        window.OnClosing().ShouldBeFalse();
    }

    [Fact]
    public async Task CloseWindow_Approved_ClosesForGood()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);
        var window = backend.Services.GetRequiredService<WindowCoordinator>();
        window.SetUnsavedChanges(true);

        using var response = await backend.Client.PostAsync(new Uri("/api/v1/native/window/close", UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        bridge.Calls.ShouldContain("close");
        window.OnClosing().ShouldBeFalse();
    }

    [Fact]
    public async Task ActivateOwner_ProjectOpenInAnotherInstance_BringsItToTheFront()
    {
        var profile = Directory.CreateTempSubdirectory("erable-profile-").FullName;
        try
        {
            var owner = new FakeBridge();
            await using var first = await StartDesktopAsync(owner, profile);
            var project = await first.CreateProjectAsync();
            var folder = first.Services.GetRequiredService<Projects.ProjectWorkspace>().Find(project.Id)!.Project.Folder;

            var activated = await InstanceRegistry.ActivateOwnerAsync(profile, folder, null, Ct);
            var other = await InstanceRegistry.ActivateOwnerAsync(profile, Path.Combine(profile, "ailleurs"), null, Ct);

            activated.ShouldBeTrue();
            other.ShouldBeFalse();
            owner.Calls.Count(c => c == "front").ShouldBe(1);
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }

    [Fact]
    public async Task Activate_WithoutTheIpcToken_Returns401()
    {
        var bridge = new FakeBridge();
        await using var backend = await StartDesktopAsync(bridge);

        using var response = await backend.Client.PostAsJsonAsync(InstanceRegistry.ActivatePath, new ActivateRequest(null), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        bridge.Calls.ShouldNotContain("front");
    }

    [Fact]
    public async Task Stop_Desktop_RemovesItsInstanceFile()
    {
        var profile = Directory.CreateTempSubdirectory("erable-profile-").FullName;
        try
        {
            await using (var backend = await StartDesktopAsync(new FakeBridge(), profile))
            {
                Directory.GetFiles(Path.Combine(profile, "instances")).Length.ShouldBe(1);
            }

            Directory.GetFiles(Path.Combine(profile, "instances")).ShouldBeEmpty();
        }
        finally
        {
            Directory.Delete(profile, recursive: true);
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }
}
