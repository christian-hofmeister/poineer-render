using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class FileSingleInstanceLockFactory : ISingleInstanceLockFactory
{
    public ISingleInstanceLock Create(string lockFilePath)
        => new FileSingleInstanceLock(lockFilePath);
}
