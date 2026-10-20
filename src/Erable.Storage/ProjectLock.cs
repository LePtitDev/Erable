using System.Globalization;
using System.Text;
using Erable.Core;

namespace Erable.Storage;

/// <summary>
/// Single-instance lock of a project (F16.R2): <c>erable.lock</c> opened without sharing for as long
/// as the project is open. Windows enforces it with a sharing violation, Linux and macOS with an exclusive
/// <c>flock</c> (how .NET implements <see cref="FileShare.None"/>). The operating system releases it when the process
/// dies, so a crash never leaves a stale lock. The file holds the PID for diagnostics only
/// and is deleted when the lock is released.
/// </summary>
public sealed class ProjectLock : IDisposable
{
    private readonly FileStream _stream;

    private ProjectLock(FileStream stream)
    {
        _stream = stream;
    }

    public static Result<ProjectLock> Acquire(string folder)
    {
        var path = Path.Combine(folder, ProjectFiles.Lock);
        FileStream stream;
        try
        {
            stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        }
        catch (IOException)
        {
            return new DomainError(StorageErrors.Locked, "The project is already open in another instance.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return new DomainError(StorageErrors.Inaccessible, ex.Message);
        }

        stream.SetLength(0);
        var content = Encoding.UTF8.GetBytes(Environment.ProcessId.ToString(CultureInfo.InvariantCulture) + "\n");
        stream.Write(content);
        stream.Flush();
        return new ProjectLock(stream);
    }

    /// <summary>Releases the lock; the file is deleted on close (<see cref="FileOptions.DeleteOnClose"/>).</summary>
    public void Dispose() => _stream.Dispose();
}
