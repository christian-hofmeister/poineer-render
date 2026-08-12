using System.Text;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

// Cross-process, cross-platform exclusive lock backed by an advisory OS-level byte-range
// file lock (FileStream.Lock/Unlock). Non-blocking: TryAcquire() fails fast instead of
// waiting for the lock, mirroring `flock -n` semantics.
//
// Note: FileStream.Lock is scoped per open file handle across process boundaries (verified
// against separate OS processes), which is exactly the scenario a cron-triggered renderer
// needs to guard against. It is not a reliable way to simulate "two instances" via two
// handles opened from within a single process/test.
public sealed class FileSingleInstanceLock : ISingleInstanceLock
{
    private const long LockRegionLength = 1;

    private readonly string _lockFilePath;
    private FileStream? _lockStream;

    public FileSingleInstanceLock(string lockFilePath)
    {
        if (string.IsNullOrWhiteSpace(lockFilePath))
            throw new ArgumentException("Lock file path must be set.", nameof(lockFilePath));

        _lockFilePath = lockFilePath;
    }

    public bool TryAcquire()
    {
        if (_lockStream is not null)
            return true; // already acquired by this instance

        var directory = Path.GetDirectoryName(_lockFilePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var stream = new FileStream(
            _lockFilePath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite);

        try
        {
            // Advisory, non-blocking OS-level lock. Throws IOException immediately if
            // another process already holds the lock.
            stream.Lock(0, LockRegionLength);
        }
        catch (IOException)
        {
            stream.Dispose();
            return false;
        }

        WriteOwnerInfo(stream);

        _lockStream = stream;
        return true;
    }

    public void Dispose()
    {
        if (_lockStream is null)
            return;

        try
        {
            _lockStream.Unlock(0, LockRegionLength);
        }
        catch (IOException)
        {
            // Best effort; disposing the stream below releases the OS-level lock regardless.
        }

        _lockStream.Dispose();
        _lockStream = null;
    }

    private static void WriteOwnerInfo(FileStream stream)
    {
        // Purely informational: makes it easy to inspect who currently holds the lock
        // (e.g. `cat poineer-render.lock` on the VPS). Not used for locking itself.
        stream.SetLength(0);
        var content = $"pid={Environment.ProcessId} startedUtc={DateTimeOffset.UtcNow:O}{Environment.NewLine}";
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }
}
