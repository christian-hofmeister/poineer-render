using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

public sealed class FileRegionScheduleStateStoreTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriteAsync_RoundTripsAndAtomicallyReplacesCompletedOrPendingState(bool pending)
    {
        await using var tempDir = TestTemporaryDirectories.Create("schedule-roundtrip", false);
        var path = Path.Combine(tempDir.DirectoryPath, "berlin", "schedule-state.json");
        var store = new FileRegionScheduleStateStore(NullLogger<FileRegionScheduleStateStore>.Instance);
        var now = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var original = new RegionScheduleState("berlin", "https://example.com/berlin.pbf", now, now.AddDays(7), false);
        await store.WriteAsync(path, original);
        var replacement = original with { ProcessingPending = pending, NextDueUtc = now.AddDays(14) };

        await store.WriteAsync(path, replacement);
        var reloaded = await new FileRegionScheduleStateStore(NullLogger<FileRegionScheduleStateStore>.Instance).ReadAsync(path);

        reloaded.Should().Be(replacement);
        Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tmp").Should().BeEmpty();
    }

    [Theory]
    [InlineData("{ incomplete")]
    [InlineData("{}")]
    [InlineData("{\"regionId\":\"berlin\",\"pbfUrl\":\"https://example.com/berlin.pbf\"}")]
    public async Task ReadAsync_ReturnsNull_ForCorruptOrIncompleteState(string json)
    {
        await using var tempDir = TestTemporaryDirectories.Create("schedule-corrupt", false);
        var path = Path.Combine(tempDir.DirectoryPath, "schedule-state.json");
        TestFiles.WriteAllText(path, json);
        var store = new FileRegionScheduleStateStore(NullLogger<FileRegionScheduleStateStore>.Instance);

        (await store.ReadAsync(path)).Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_HandlesMissingState_AndPendingFirstAttemptWithoutSuccessfulCheck()
    {
        await using var tempDir = TestTemporaryDirectories.Create("schedule-first-attempt", false);
        var path = Path.Combine(tempDir.DirectoryPath, "schedule-state.json");
        var store = new FileRegionScheduleStateStore(NullLogger<FileRegionScheduleStateStore>.Instance);
        (await store.ReadAsync(path)).Should().BeNull();
        var pending = new RegionScheduleState("berlin", "https://example.com/berlin.pbf", null, null, true);

        await store.WriteAsync(path, pending);

        (await store.ReadAsync(path)).Should().Be(pending);
    }
}
