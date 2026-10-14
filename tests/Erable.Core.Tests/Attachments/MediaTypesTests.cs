using System.Text;
using Erable.Core.Attachments;

namespace Erable.Core.Tests.Attachments;

public sealed class MediaTypesTests
{
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13];

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { (byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a' }, "image/gif")]
    [InlineData(new byte[] { (byte)'%', (byte)'P', (byte)'D', (byte)'F', (byte)'-', (byte)'1' }, "application/pdf")]
    [InlineData(new byte[] { (byte)'P', (byte)'K', 3, 4, 0, 0 }, "application/zip")]
    [InlineData(new byte[] { 0x1A, 0x45, 0xDF, 0xA3, 0 }, "video/webm")]
    public void Detect_KnownSignature_ReturnsItsType(byte[] head, string expected) =>
        MediaTypes.Detect(head, "file.bin").ShouldBe(expected);

    [Fact]
    public void Detect_PngNamedTxt_TrustsTheSignature() =>
        MediaTypes.Detect(Png, "notes.txt").ShouldBe("image/png");

    [Fact]
    public void Detect_WebpRiff_ReturnsWebp() =>
        MediaTypes.Detect("RIFF\0\0\0\0WEBPVP8 "u8, "x").ShouldBe("image/webp");

    [Fact]
    public void Detect_Mp4Brand_ReturnsMp4() =>
        MediaTypes.Detect([0, 0, 0, 0x18, .. "ftypisom"u8], "x").ShouldBe("video/mp4");

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"/>", "blason.png")]
    [InlineData("<?xml version=\"1.0\"?>\n<svg/>", "blason.svg")]
    [InlineData("\uFEFF  <SVG onload=alert(1)>", "image.jpg")]
    public void Detect_SvgContent_IsSvgWhateverTheExtension(string content, string fileName) =>
        MediaTypes.Detect(Encoding.UTF8.GetBytes(content), fileName).ShouldBe("image/svg+xml");

    [Theory]
    [InlineData("<!DOCTYPE html><p>x")]
    [InlineData("<html><body>")]
    [InlineData("hello <script>alert(1)</script>")]
    public void Detect_HtmlContent_IsHtml(string content) =>
        MediaTypes.Detect(Encoding.UTF8.GetBytes(content), "readme.txt").ShouldBe("text/html");

    [Theory]
    [InlineData("notes.txt", "text/plain")]
    [InlineData("data.CSV", "text/csv")]
    [InlineData("unknown.xyz", MediaTypes.OctetStream)]
    [InlineData("no-extension", MediaTypes.OctetStream)]
    public void Detect_PlainText_FallsBackToExtension(string fileName, string expected) =>
        MediaTypes.Detect("plain words"u8, fileName).ShouldBe(expected);

    [Theory]
    [InlineData("image/png", AttachmentDisplay.Image)]
    [InlineData("video/mp4", AttachmentDisplay.Video)]
    [InlineData("audio/mpeg", AttachmentDisplay.Audio)]
    [InlineData("application/pdf", AttachmentDisplay.Pdf)]
    [InlineData("image/svg+xml", AttachmentDisplay.File)]
    [InlineData("text/html", AttachmentDisplay.File)]
    [InlineData("application/zip", AttachmentDisplay.File)]
    public void DisplayOf_MediaType_FollowsF06R2(string mediaType, AttachmentDisplay expected) =>
        MediaTypes.DisplayOf(mediaType).ShouldBe(expected);

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/xhtml+xml")]
    [InlineData("application/xml")]
    public void IsInlineSafe_Markup_IsFalse(string mediaType) => MediaTypes.IsInlineSafe(mediaType).ShouldBeFalse();

    [Fact]
    public void Detect_EmptyFile_UsesExtension() => MediaTypes.Detect([], "vide.pdf").ShouldBe("application/pdf");
}

public sealed class AttachmentFileNameTests
{
    [Theory]
    [InlineData(@"C:\fakepath\portrait.png", "portrait.png")]
    [InlineData("dir/sub/plans.zip", "plans.zip")]
    [InlineData("  carte.webp ", "carte.webp")]
    public void Normalize_PathOrSpaces_KeepsTheFileName(string input, string expected) =>
        AttachmentFileName.Normalize(input).Value.ShouldBe(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("folder/")]
    public void Normalize_NoName_Fails(string? input) =>
        AttachmentFileName.Normalize(input).Error.Code.ShouldBe(ErrorCodes.NameEmpty);
}
