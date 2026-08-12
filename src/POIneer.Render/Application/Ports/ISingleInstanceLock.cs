namespace POIneer.Render.Application.Ports;

// Coordinates exclusive execution across process boundaries so that a scheduled
// (e.g. cron-triggered) run never overlaps with an already-running instance.
public interface ISingleInstanceLock : IDisposable
{
    // Attempts to acquire the lock without blocking.
    // Returns true if this instance now holds the lock, false if another instance already holds it.
    bool TryAcquire();
}
