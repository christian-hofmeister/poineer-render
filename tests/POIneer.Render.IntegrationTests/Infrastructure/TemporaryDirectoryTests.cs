using Xunit;
using POIneer.Render.Infrastructure.FileSystem;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public sealed class TemporaryDirectoryTests
{
    [Fact]
    public async Task Create_CreatesDirectory()
    {
        await using var dir = TemporaryDirectory.Create("poineer_test_");
        var path = dir.DirectoryPath;

        Assert.True(Directory.Exists(path));
        Assert.Contains("poineer_test_", Path.GetFileName(path));
    }


    [Fact]
    public async Task DisposeAsync_DeletesDirectory_ByDefault()
    {
        string path;

        await using (var dir = TemporaryDirectory.Create("poineer_test_"))
        {
            path = dir.DirectoryPath;

            File.WriteAllText(Path.Combine(path, "x.txt"), "data");
            Assert.True(Directory.Exists(path));
        }

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotDeleteDirectory_WhenKeepIsTrue()
    {
        string path;

        await using (var dir = TemporaryDirectory.Create("poineer_test_", keepOnDispose: true))
        {
            path = dir.DirectoryPath;

            File.WriteAllText(Path.Combine(path, "x.txt"), "data");
        }

        Assert.True(Directory.Exists(path));

        // cleanup to avoid leaking temp dirs
        Directory.Delete(path, recursive: true);
    }

    [Fact]
    public async Task DisposeAsync_CanBeCalledMultipleTimes()
    {
        var dir = TemporaryDirectory.Create("poineer_test_");
        var path = dir.DirectoryPath;

        File.WriteAllText(Path.Combine(path, "x.txt"), "data");

        await dir.DisposeAsync();
        await dir.DisposeAsync(); // should not throw

        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public async Task DisposeAsync_DoesNotThrow_WhenDirectoryAlreadyMissing()
    {
        var dir = TemporaryDirectory.Create("poineer_test_");
        var path = dir.DirectoryPath;

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        Assert.False(Directory.Exists(path));

        // Should not throw even if directory is already gone
        await dir.DisposeAsync();
    }
}
