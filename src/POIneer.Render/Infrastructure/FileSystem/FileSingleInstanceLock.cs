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

    // FileStream.Lock does not expose a dedicated exception type for "already locked", so
    // TryAcquire() has to recognize the failure by HResult. These are the signatures for a
    // genuine lock conflict - Windows' ERROR_SHARING_VIOLATION/ERROR_LOCK_VIOLATION (mapped
    // to HRESULT), and the raw Unix errno values fcntl(F_SETLK) is allowed to return for a
    // held lock (EAGAIN, empirically confirmed against .NET on Linux; EACCES per POSIX).
    // Anything else is treated as a real, unexpected failure and rethrown, rather than being
    // swallowed as "another instance is running" - that would make Runner log a misleading
    // skip message and mask a genuine operational problem.
    private static readonly HashSet<int> LockConflictHResults =
    [
        unchecked((int)0x80070020), // Windows ERROR_SHARING_VIOLATION
        unchecked((int)0x80070021), // Windows ERROR_LOCK_VIOLATION
        11,                          // Unix EAGAIN/EWOULDBLOCK
        13,                          // Unix EACCES
    ];

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
        catch (IOException ex) when (LockConflictHResults.Contains(ex.HResult))
        {
            stream.Dispose();
            return false;
        }
        catch
        {
            // Not a recognized lock-conflict signature - an unexpected I/O failure
            // (unsupported filesystem semantics, disk error, etc.). Release what we opened
            // and let it propagate instead of masking it as "another instance is running".
            stream.Dispose();
            throw;
        }

        try
        {
            WriteOwnerInfo(stream);
        }
        catch (IOException)
        {
            // Purely informational (see WriteOwnerInfo). The lock itself is already held,
            // so a failed cosmetic write must not fail acquisition.
        }

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
