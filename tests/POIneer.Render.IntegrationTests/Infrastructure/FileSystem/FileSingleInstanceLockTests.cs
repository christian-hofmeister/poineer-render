using FluentAssertions;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

// Exercises the real, OS-level file lock. Note: FileStream.Lock is scoped per open file
// handle across separate processes, which is what actually matters for two overlapping
// cron-triggered POIneer.Render runs. Two handles opened from within the SAME process are
// not a reliable way to reproduce that on every platform (advisory locks on Linux are
// associated with the owning process, so a second handle from the same process does not
// conflict). These tests therefore focus on what is reliably observable in-process:
// acquiring, persisting owner metadata, and releasing so a later instance can acquire again.
public sealed class FileSingleInstanceLockTests
{
    [Fact]
    public async Task TryAcquire_ReturnsTrue_AndCreatesLockFile_WhenNoOtherInstanceHoldsIt()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("lock-acquire", false);
        var lockFilePath = Path.Combine(tempDir.DirectoryPath, "poineer-render.lock");
        using var sut = new FileSingleInstanceLock(lockFilePath);

        // Act
        var acquired = sut.TryAcquire();

        // Assert
        acquired.Should().BeTrue();
        File.Exists(lockFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquire_CreatesMissingLockFileDirectory()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("lock-creates-directory", false);
        var lockFilePath = Path.Combine(tempDir.DirectoryPath, "nested", "poineer-render.lock");
        using var sut = new FileSingleInstanceLock(lockFilePath);

        // Act
        var acquired = sut.TryAcquire();

        // Assert
        acquired.Should().BeTrue();
        File.Exists(lockFilePath).Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquire_WritesOwnerMetadata_ToTheLockFile()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("lock-owner-metadata", false);
        var lockFilePath = Path.Combine(tempDir.DirectoryPath, "poineer-render.lock");
        using var sut = new FileSingleInstanceLock(lockFilePath);

        // Act
        sut.TryAcquire();
        sut.Dispose(); // release so the file can be read without a sharing conflict

        // Assert
        var content = await File.ReadAllTextAsync(lockFilePath);
        content.Should().Contain($"pid={Environment.ProcessId}");
    }

    [Fact]
    public async Task Dispose_ReleasesTheLock_SoALaterInstanceCanAcquireIt()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("lock-release-allows-reacquire", false);
        var lockFilePath = Path.Combine(tempDir.DirectoryPath, "poineer-render.lock");

        var first = new FileSingleInstanceLock(lockFilePath);
        first.TryAcquire().Should().BeTrue();

        // Act
        first.Dispose();

        using var second = new FileSingleInstanceLock(lockFilePath);
        var acquiredBySecond = second.TryAcquire();

        // Assert
        acquiredBySecond.Should().BeTrue();
    }

    [Fact]
    public async Task Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("lock-dispose-idempotent", false);
        var lockFilePath = Path.Combine(tempDir.DirectoryPath, "poineer-render.lock");
        var sut = new FileSingleInstanceLock(lockFilePath);
        sut.TryAcquire();

        // Act
        var act = () =>
        {
            sut.Dispose();
            sut.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
