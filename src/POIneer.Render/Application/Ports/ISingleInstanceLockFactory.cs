namespace POIneer.Render.Application.Ports;

public interface ISingleInstanceLockFactory
{
    // Creates a lock bound to the given lock file path. The lock is not acquired yet;
    // call TryAcquire() on the returned instance.
    ISingleInstanceLock Create(string lockFilePath);
}
