using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Erable.Core;
using Erable.Core.Attachments;
using Erable.Core.Elements;
using Erable.Server.Api;
using Erable.Server.Tests.Infrastructure;

namespace Erable.Server.Tests;

public sealed class AttachmentsApiTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4, 5, 6, 7, 8];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Uri Url(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task Upload_Png_CreatesAttachmentListedOnThePage()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await backend.UploadAsync(project.Id, page.Id, "carte.png", Png);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var item = await response.ReadAsync<AttachmentItem>();
        item.ShouldBe(new AttachmentItem(item.Id, page.Id, "carte.png", "image/png", Png.Length, AttachmentDisplay.Image));
        var view = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}"), Ct)).ReadAsync<PageView>();
        view.Attachments.ShouldBe([item]);
    }

    [Fact]
    public async Task Download_Image_ServedInlineWithSafetyHeaders()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "carte.png", Png)).ReadAsync<AttachmentItem>();

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/attachments/{item.Id}"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.ETag!.IsWeak.ShouldBeFalse();
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Png);
    }

    [Fact]
    public async Task Download_Range_Returns206WithTheSegment()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "carte.png", Png)).ReadAsync<AttachmentItem>();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/projects/{project.Id}/attachments/{item.Id}");
        request.Headers.Range = new RangeHeaderValue(4, 9);

        using var response = await backend.Client.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.PartialContent);
        response.Content.Headers.ContentRange!.ToString().ShouldBe($"bytes 4-9/{Png.Length}");
        (await response.Content.ReadAsByteArrayAsync(Ct)).ShouldBe(Png[4..10]);
    }

    [Theory]
    [InlineData("blason.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("page.html", "<!doctype html><script>alert(1)</script>")]
    [InlineData("deguise.png", "<svg onload=alert(1)>")]
    public async Task Download_Markup_ForcedToOctetStreamDownload(string fileName, string content)
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, fileName, Encoding.UTF8.GetBytes(content))).ReadAsync<AttachmentItem>();

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/attachments/{item.Id}"), Ct);

        item.Display.ShouldBe(AttachmentDisplay.File);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileNameStar.ShouldBe(fileName);
    }

    [Fact]
    public async Task Upload_OverTheLimit_Returns413AndCreatesNothing()
    {
        await using var backend = await TestBackend.StartAsync(o => o with { MaxAttachmentSize = 1024 });
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await backend.UploadAsync(project.Id, page.Id, "gros.bin", new byte[4096]);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        (await response.ProblemCodeAsync()).ShouldBe(ErrorCodes.AttachmentTooLarge);
        var view = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/pages/{page.Id}"), Ct)).ReadAsync<PageView>();
        view.Attachments.ShouldBeEmpty();
    }

    [Fact]
    public async Task Upload_NotMultipart_Returns415()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");

        using var response = await backend.Client.PostAsync(
            Url($"/api/v1/projects/{project.Id}/pages/{page.Id}/attachments"), new ByteArrayContent(Png), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Upload_ToAFolder_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var folder = await backend.CreateFolderAsync(project.Id, Roots.Pages.Id, "Lieux");

        using var response = await backend.UploadAsync(project.Id, folder.Id, "carte.png", Png);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Download_UnknownId_Returns404()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();

        using var response = await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/attachments/3F2A9C14E07B5D21"), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Resolve_Attachment_IncludesItsDisplayAndOwningPage()
    {
        await using var backend = await TestBackend.StartAsync();
        var project = await backend.CreateProjectAsync();
        var page = await backend.CreatePageAsync(project.Id, Roots.Pages.Id, "Hautbois");
        var item = await (await backend.UploadAsync(project.Id, page.Id, "carte.png", Png)).ReadAsync<AttachmentItem>();

        var result = await (await backend.Client.GetAsync(Url($"/api/v1/projects/{project.Id}/resolve?ids={item.Id}"), Ct)).ReadAsync<ResolveResult>();

        var resolved = result.Items.Single();
        resolved.Kind.ShouldBe(ElementKind.Attachment);
        resolved.Attachment.ShouldBe(item);
        resolved.Path[^1].Name.ShouldBe("Hautbois");
    }
}
