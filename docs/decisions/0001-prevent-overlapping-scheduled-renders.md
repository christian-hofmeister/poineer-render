# ADR 0001: Prevent Overlapping Scheduled Renders

## Status

Accepted

## Context

POIneer.Render is triggered on the VPS through a cron schedule (see #110 for the
production configuration groundwork). A single render run can take longer than
expected (large PBF downloads, OSM processing), so a slow or hanging run could
still be executing when the next scheduled run starts. Two overlapping instances
writing into the same work/output directories at the same time is unsafe.

Issue #108 asked for a locking mechanism so that:

- a second renderer start is skipped while one instance is running,
- scheduled execution remains safe,
- logs show when an execution was skipped.

The issue's proposed solution used `flock -n` directly in the crontab entry:

```text
0 3 * * * flock -n /opt/poineer-render/poineer-render.lock /opt/dotnet/current/dotnet /opt/poineer-render/app/POIneer.Render.dll >> /opt/poineer-render/logs/render.log 2>&1
```

This reliably prevents overlap, but `flock -n` does not emit anything on its own
when it skips a run - by default it exits silently with a non-zero status. Meeting
the "logs show when execution was skipped" acceptance criterion would require
extra shell scripting around the raw crontab line.

## Decision

Implement the lock inside POIneer.Render itself rather than purely at the
cron/shell level:

- `ISingleInstanceLock` / `ISingleInstanceLockFactory` (`Application/Ports`)
  define the exclusivity contract.
- `FileSingleInstanceLock` (`Infrastructure/FileSystem`) implements it using
  `FileStream.Lock`/`Unlock`, a non-blocking, OS-level advisory byte-range lock
  that .NET enforces across separate processes on both Linux and Windows.
- `Runner` acquires the lock right after resolving paths (skipping it entirely
  when `DryRun` is set), logs a clear warning through the existing `ILogger`
  pipeline when the lock could not be acquired, and returns exit code `0` (a
  skip is expected behavior, not a failure).

Because the lock now lives in the application, the crontab entry no longer needs
`flock`:

```text
0 3 * * * /opt/dotnet/current/dotnet /opt/poineer-render/app/POIneer.Render.dll >> /opt/poineer-render/logs/render.log 2>&1
```

`Renderer:LockFilePath` in `appsettings.Production.json` points at the same
`/opt/poineer-render/poineer-render.lock` path the original issue proposed, so
the lock file stays inspectable on the VPS the same way (`cat` it to see which
PID currently holds it).

## Consequences

- Works regardless of how the process is started (cron, a manual run, or a
  future systemd timer) - not just when wrapped in `flock`.
- Skipped executions are visible in `render.log` with a clear message, directly
  satisfying the acceptance criteria.
- Testable: `Runner`'s skip/log/return-0 behavior is covered by unit tests with
  a mocked lock, and `FileSingleInstanceLock` is covered by integration tests
  exercising the real OS lock.
- `FileStream.Lock` is advisory and scoped per open file handle across process
  boundaries; this was verified manually with two independent OS processes
  (one holding the lock while a second attempt is denied, then succeeds after
  release). On Linux, the underlying advisory lock is associated with the
  owning process, so two handles opened from within the *same* process do not
  conflict with each other - this is a known platform nuance and not
  meaningfully testable in-process on Linux. Automated coverage therefore
  focuses on acquire/release/reacquire behavior rather than simulating
  cross-process contention with two handles in a single process.
- No rework needed if the deployment story evolves later (e.g. systemd
  timers): the guard travels with the binary instead of living in ops scripts.

## References

- #108 - Ensure that POIneer.Render cannot run multiple times in parallel on the VPS
- #110 - Renderer scheduled VPS config groundwork
