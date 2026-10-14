using System.Collections.Concurrent;
using System.Security.Cryptography;
using Erable.Core.Attachments;

namespace Erable.Storage;

/// <summary>A blob just written: its content address, size and first bytes (media type detection).</summary>
/// <remarks>
/// Dispose it once the database row that references the blob is committed: until then the lease protects the blob
/// from the garbage collector (<see cref="IBlobStore.IsLeased"/>).
/// </remarks>
public sealed class StoredBlob(string sha256, long size, byte[] head, Action release) : IDisposable
{
    private Action? _release = release;

    /// <summary>Lower-case hexadecimal SHA-256 of the content.</summary>
    public string Sha256 { get; } = sha256;

    public long Size { get; } = size;

    /// <summary>Up to <see cref="MediaTypes.SniffLength"/> first bytes.</summary>
    public ReadOnlyMemory<byte> Head { get; } = head;

    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}

/// <summary>Content-addressed storage of attachment files (docs/architecture.md §7.3). Disk in v1, S3 possible later.</summary>
public interface IBlobStore
{
    /// <summary>
    /// Streams <paramref name="content"/> into the store. The blob only becomes visible, under its SHA-256, once it is
    /// complete and flushed to disk: an interrupted or refused write leaves nothing behind. Returns null when the content
    /// exceeds <paramref name="maxSize"/> bytes (F06.R5).
    /// </summary>
    Task<StoredBlob?> WriteAsync(Stream content, long maxSize, CancellationToken cancellationToken = default);

    /// <summary>A seekable read stream (HTTP <c>Range</c>), or null when the blob does not exist.</summary>
    Stream? OpenRead(string sha256);

    bool Exists(string sha256);

    /// <summary>Hashes of every stored blob.</summary>
    IEnumerable<string> List();

    void Delete(string sha256);

    /// <summary>True while a write of this blob is not yet referenced by a committed row.</summary>
    bool IsLeased(string sha256);
}

/// <summary>
/// Blobs on disk under <c>blobs/ab/cd/&lt;sha256&gt;</c>: written to a temporary file, flushed (<c>fsync</c>), then
/// atomically renamed (docs/testing.md §6.1). Identical contents are stored once.
/// </summary>
public sealed class DiskBlobStore(string directory) : IBlobStore
{
    private const string TemporaryFolder = "tmp";
    private const int BufferSize = 81920;

    private readonly ConcurrentDictionary<string, int> _leases = new(StringComparer.Ordinal);

    public string Directory { get; } = directory;

    public static bool IsValidHash(string? sha256) =>
        sha256 is { Length: 64 } && sha256.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    public string PathOf(string sha256)
    {
        if (!IsValidHash(sha256))
        {
            throw new ArgumentException($"'{sha256}' is not a lower-case SHA-256.", nameof(sha256));
        }

        return Path.Combine(Directory, sha256[..2], sha256[2..4], sha256);
    }

    public async Task<StoredBlob?> WriteAsync(Stream content, long maxSize, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        var temporaryDirectory = Path.Combine(Directory, TemporaryFolder);
        System.IO.Directory.CreateDirectory(temporaryDirectory);
        var temporary = Path.Combine(temporaryDirectory, Guid.NewGuid().ToString("N"));
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var head = new MemoryStream();
            long size = 0;
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, useAsync: true))
            {
                var buffer = new byte[BufferSize];
                int read;
                while ((read = await content.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    size += read;
                    if (size > maxSize)
                    {
                        return null;
                    }

                    if (head.Length < MediaTypes.SniffLength)
                    {
                        head.Write(buffer, 0, (int)Math.Min(read, MediaTypes.SniffLength - head.Length));
                    }

                    hash.AppendData(buffer, 0, read);
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                // fsync: the rename below must never publish a file whose content is still in the OS cache only.
                file.Flush(flushToDisk: true);
            }

            var sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            var lease = Lease(sha256);
            try
            {
                var target = PathOf(sha256);
                if (File.Exists(target))
                {
                    // Deduplication: same content, same name; the existing file is kept.
                    File.Delete(temporary);
                }
                else
                {
                    System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    MoveAtomically(temporary, target);
                }
            }
            catch
            {
                lease();
                throw;
            }

            return new StoredBlob(sha256, size, head.ToArray(), lease);
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    public Stream? OpenRead(string sha256)
    {
        var path = PathOf(sha256);
        return File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, BufferSize, useAsync: true)
            : null;
    }

    public bool Exists(string sha256) => IsValidHash(sha256) && File.Exists(PathOf(sha256));

    public IEnumerable<string> List()
    {
        if (!System.IO.Directory.Exists(Directory))
        {
            return [];
        }

        return System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories)
            .Where(f => !IsTemporary(f))
            .Select(Path.GetFileName)
            .Where(IsValidHash)
            .Select(f => f!)
            .ToList();
    }

    public void Delete(string sha256) => TryDelete(PathOf(sha256));

    public bool IsLeased(string sha256) => _leases.ContainsKey(sha256);

    /// <summary>Files left in the temporary folder by a crash; safe to delete when no write is running.</summary>
    public IEnumerable<string> ListTemporaryFiles()
    {
        var temporaryDirectory = Path.Combine(Directory, TemporaryFolder);
        return System.IO.Directory.Exists(temporaryDirectory) ? System.IO.Directory.EnumerateFiles(temporaryDirectory).ToList() : [];
    }

    /// <summary>Removes the leftovers of interrupted writes; call it only when no write is running.</summary>
    public void DeleteTemporaryFiles()
    {
        foreach (var file in ListTemporaryFiles())
        {
            TryDelete(file);
        }
    }

    private Action Lease(string sha256)
    {
        _leases.AddOrUpdate(sha256, 1, (_, count) => count + 1);
        return () =>
        {
            if (_leases.AddOrUpdate(sha256, 0, (_, count) => count - 1) <= 0)
            {
                _leases.TryRemove(new KeyValuePair<string, int>(sha256, 0));
            }
        };
    }

    private static bool IsTemporary(string file) =>
        string.Equals(Path.GetFileName(Path.GetDirectoryName(file)), TemporaryFolder, StringComparison.Ordinal);

    private static void MoveAtomically(string source, string target)
    {
        try
        {
            File.Move(source, target, overwrite: false);
        }
        catch (IOException) when (File.Exists(target))
        {
            // A concurrent write of the same content won the race: its file is identical.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: a leftover temporary file is reported and removed by `erable doctor --fix`.
        }
    }
}
