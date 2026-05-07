using Xunit;
using POIneer.Render.TestHelpers;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public sealed class TemporaryDirectoryTests
{
    [Fact]
    public async Task Create_CreatesDirectory()
    {
        await using var tempDir = TestTemporaryDirectories.Create("CreateCreatesDirectory", false);
        var path = tempDir.DirectoryPath;

        Assert.True(Directory.Exists(path));

        Assert.StartsWith(
            Path.Combine(Path.GetTempPath(), "poineer-tests"),
            path);

        Assert.Contains("CreateCreatesDirectory", Path.GetFileName(path));
    }


    [Fact]
    public async Task DisposeAsync_DeletesDirectory_ByDefault()
    {
        string path;

        await using (var tempDir = TestTemporaryDirectories.Create("DisposeAsyncDeletesDirectoryByDefault"))
        {
            path = tempDir.DirectoryPath;

            File.WriteAllText(Path.Combine(path, "x.txt"), "data");
            Assert.True(Directory.Exists(path));
        }

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDeleteDirectory_WhenKeepIsTrue()
    {
        string path;

        await using var tempDir = TestTemporaryDirectories.Create("DisposeAsyncDoesNotDeleteDirectoryWhenKeepIsTrue", true);
        {
            path = tempDir.DirectoryPath;

            File.WriteAllText(Path.Combine(path, "x.txt"), "data");
        }

        Assert.True(Directory.Exists(path));

        // cleanup to avoid leaking temp dirs
        Directory.Delete(path, recursive: true);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        await using var tempDir = TestTemporaryDirectories.Create("DisposeAsyncCanBeCalledMultipleTimes", false);
        var path = tempDir.DirectoryPath;

        File.WriteAllText(Path.Combine(path, "x.txt"), "data");

        await tempDir.DisposeAsync();
        await tempDir.DisposeAsync(); // should not throw

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenDirectoryAlreadyMissing()
    {
        await using var tempDir = TestTemporaryDirectories.Create("DisposeAsyncDoesNotThrowWhenDirectoryAlreadyMissing", false);
        var path = tempDir.DirectoryPath;

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Assert.False(Directory.Exists(path));

        // Should not throw even if directory is already gone
        await tempDir.DisposeAsync();
    }
}
