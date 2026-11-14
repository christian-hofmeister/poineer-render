using FluentAssertions;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

public sealed class DiskSpaceReaderTests
{
    [Fact]
    public async Task GetAvailableBytes_UsesExistingParentWhenTargetDoesNotExist()
    {
        await using var tempDir = TestTemporaryDirectories.Create("disk-space-reader", false);
        var target = Path.Combine(tempDir.DirectoryPath, "region", "output");
        var reader = new DiskSpaceReader();

        var availableBytes = reader.GetAvailableBytes(target);

        availableBytes.Should().BeGreaterThanOrEqualTo(0);
        Directory.Exists(target).Should().BeFalse();
    }
}
