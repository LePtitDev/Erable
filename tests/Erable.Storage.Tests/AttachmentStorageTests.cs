using Dapper;
using Erable.Core;
using Erable.Core.Elements;
using Erable.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Erable.Storage.Tests;

public sealed class AttachmentStorageTests : IDisposable
{
    private const long Limit = 1024 * 1024;
    private static readonly byte[] Png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];

    private readonly TempDirectory _dir = TempDirectory.Create();
    private readonly OpenedProject _project;
    private readonly ProjectService _service;

    public AttachmentStorageTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 8, 0, 0, TimeSpan.Zero));
        _project = ProjectStore.Create(_dir.Path, "Atlas", "fr", clock).Value;
        _service = new ProjectService(_project, clock);
    }

    public void Dispose()
    {
        _project.Dispose();
        _dir.Dispose();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<ElementId> PageAsync(string name) =>
        (await _service.CreateElementAsync(ElementKind.Page, Roots.Pages.Id, name, null, "local", cancellationToken: Ct)).Value.Id;

    private Task<Result<AttachmentRecord>> AttachAsync(ElementId page, string fileName, byte[] content) =>
        _service.AddAttachmentAsync(page, fileName, new MemoryStream(content), Limit, "local", Ct);

    private string[] BlobFiles() =>
        [.. Directory.EnumerateFiles(_project.BlobsDirectory, "*", SearchOption.AllDirectories).Select(Path.GetFileName).Select(f => f!)];

    [Fact]
    public async Task AddAttachment_SameFileOnTwoPages_TwoAttachmentsOneBlob()
    {
        var ilsa = await PageAsync("Ilsa Varenne");
        var hautbois = await PageAsync("Hautbois");

        var first = (await AttachAsync(ilsa, "portrait.png", Png)).Value;
        var second = (await AttachAsync(hautbois, "copie.png", Png)).Value;

        first.Id.ShouldNotBe(second.Id);
        first.Sha256.ShouldBe(second.Sha256);
        _project.Blobs.List().ShouldBe([first.Sha256]);
        _project.Repository.GetAttachments(ilsa).Single().FileName.ShouldBe("portrait.png");
        _project.Repository.GetAttachments(hautbois).Single().PageId.ShouldBe(hautbois);
    }

    [Fact]
    public async Task AddAttachment_Png_StoresUnderShardedHashWithDetectedType()
    {
        var page = await PageAsync("Hautbois");

        var attachment = (await AttachAsync(page, "carte.bin", Png)).Value;

        attachment.Mime.ShouldBe("image/png");
        attachment.Size.ShouldBe(Png.Length);
        var expected = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Png));
        attachment.Sha256.ShouldBe(expected);
        File.Exists(Path.Combine(_project.BlobsDirectory, expected[..2], expected[2..4], expected)).ShouldBeTrue();
        _project.Repository.GetElement(attachment.Id).ShouldNotBeNull().Kind.ShouldBe(ElementKind.Attachment);
        _project.Repository.CountRevisions(attachment.Id).ShouldBe(1);
    }

    [Fact]
    public async Task AddAttachment_TooLarge_RefusedWithoutAnyFile()
    {
        var page = await PageAsync("Hautbois");

        var result = await _service.AddAttachmentAsync(page, "gros.bin", new MemoryStream(new byte[2048]), maxSize: 1024, "local", Ct);

        result.Error.Code.ShouldBe(ErrorCodes.AttachmentTooLarge);
        BlobFiles().ShouldBeEmpty();
        _project.Repository.GetAttachments(page).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddAttachment_StreamFailsMidway_NoBlobAndNoAttachment()
    {
        var page = await PageAsync("Hautbois");

        await Should.ThrowAsync<IOException>(() =>
            _service.AddAttachmentAsync(page, "coupure.bin", new FailingStream(failAfter: 100_000), Limit, "local", Ct));

        BlobFiles().ShouldBeEmpty();
        _project.Repository.GetAttachments(page).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddAttachment_UnknownPage_StoresNothing()
    {
        var result = await AttachAsync(ElementId.Parse("3F2A9C14E07B5D21"), "x.png", Png);

        result.Error.Code.ShouldBe(ErrorCodes.ElementNotFound);
        BlobFiles().ShouldBeEmpty();
    }

    [Fact]
    public async Task CollectGarbage_BlobOnlyReferencedByRevision_IsKept()
    {
        var page = await PageAsync("Hautbois");
        var attachment = (await AttachAsync(page, "carte.png", Png)).Value;
        // A purged attachment: the row goes, its revision stays.
        _project.Database.Write((c, t) => c.Execute("DELETE FROM attachments WHERE id = @Id", new { Id = attachment.Id.ToInt64() }, t));

        var deleted = await _service.CollectGarbageAsync(Ct);

        deleted.ShouldBeEmpty();
        _project.Blobs.Exists(attachment.Sha256).ShouldBeTrue();
    }

    [Fact]
    public async Task CollectGarbage_UnreferencedBlob_IsDeleted()
    {
        using var orphan = await _project.Blobs.WriteAsync(new MemoryStream([1, 2, 3]), Limit, Ct);
        orphan.ShouldNotBeNull().Dispose();

        var deleted = await _service.CollectGarbageAsync(Ct);

        deleted.ShouldBe([orphan.Sha256]);
        _project.Blobs.Exists(orphan.Sha256).ShouldBeFalse();
    }

    [Fact]
    public async Task CollectGarbage_UploadNotYetCommitted_IsKept()
    {
        using var pending = (await _project.Blobs.WriteAsync(new MemoryStream([4, 5, 6]), Limit, Ct)).ShouldNotBeNull();

        var deleted = await _service.CollectGarbageAsync(Ct);

        deleted.ShouldBeEmpty();
        _project.Blobs.Exists(pending.Sha256).ShouldBeTrue();
    }

    [Fact]
    public async Task OpenRead_StoredBlob_IsSeekable()
    {
        using var blob = (await _project.Blobs.WriteAsync(new MemoryStream([1, 2, 3, 4, 5]), Limit, Ct)).ShouldNotBeNull();

        await using var stream = _project.Blobs.OpenRead(blob.Sha256).ShouldNotBeNull();
        stream.CanSeek.ShouldBeTrue();
        stream.Seek(3, SeekOrigin.Begin);

        stream.ReadByte().ShouldBe(4);
    }

    [Fact]
    public void DeleteTemporaryFiles_LeftoverOfACrash_IsRemoved()
    {
        var tmp = Path.Combine(_project.BlobsDirectory, "tmp");
        Directory.CreateDirectory(tmp);
        File.WriteAllBytes(Path.Combine(tmp, "partial"), [1, 2]);

        _project.Blobs.DeleteTemporaryFiles();

        _project.Blobs.ListTemporaryFiles().ShouldBeEmpty();
        _project.Blobs.List().ShouldBeEmpty();
    }

    /// <summary>Produces bytes then fails, like a dropped connection or a killed client.</summary>
    private sealed class FailingStream(int failAfter) : Stream
    {
        private int _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position >= failAfter)
            {
                throw new IOException("Connection lost.");
            }

            var n = Math.Min(count, failAfter - _position);
            Array.Fill(buffer, (byte)7, offset, n);
            _position += n;
            return n;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
