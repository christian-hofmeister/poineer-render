using FluentAssertions;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.UseCases;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases;

public sealed class RegionUpdateCheckerTests
{
    private static readonly RegionDto Berlin = new(
        Id: "berlin",
        Name: "Berlin",
        PbfUrl: "https://example.com/berlin.osm.pbf",
        Poly: "berlin.poly");

    [Fact]
    public async Task CheckAsync_ShouldRedownloadPbf_WhenNoStoredStateExists()
    {
        // Arrange
        var metadata = new RegionUpdateMetadata("\"abc\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        var stateStore = new InMemoryRegionDownloadStateStore();
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(metadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        var result = await sut.CheckAsync(Berlin, "state.json");

        // Assert
        result.ShouldRedownloadPbf.Should().BeTrue();
        result.Reason.Should().Be("No previous download state exists for this region.");
    }

    [Fact]
    public async Task CheckAsync_ShouldNotRedownloadPbf_WhenETagIsUnchanged()
    {
        // Arrange
        var remoteMetadata = new RegionUpdateMetadata("\"abc\"", DateTimeOffset.Parse("2026-01-02T00:00:00Z"), 128);
        var storedMetadata = new RegionUpdateMetadata("\"abc\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        var stateStore = new InMemoryRegionDownloadStateStore(
            new RegionDownloadState(Berlin.Id, Berlin.PbfUrl, storedMetadata, DateTimeOffset.UtcNow));
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(remoteMetadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        var result = await sut.CheckAsync(Berlin, "state.json");

        // Assert
        result.ShouldRedownloadPbf.Should().BeFalse("ETag is preferred over Last-Modified when present");
        result.Reason.Should().Be("Remote PBF metadata is unchanged.");
    }

    [Fact]
    public async Task CheckAsync_ShouldRedownloadPbf_WhenETagChanged()
    {
        // Arrange
        var remoteMetadata = new RegionUpdateMetadata("\"new\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        var storedMetadata = new RegionUpdateMetadata("\"old\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        var stateStore = new InMemoryRegionDownloadStateStore(
            new RegionDownloadState(Berlin.Id, Berlin.PbfUrl, storedMetadata, DateTimeOffset.UtcNow));
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(remoteMetadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        var result = await sut.CheckAsync(Berlin, "state.json");

        // Assert
        result.ShouldRedownloadPbf.Should().BeTrue();
        result.Reason.Should().Be("Remote ETag changed.");
    }

    [Fact]
    public async Task CheckAsync_ShouldNotRedownloadPbf_WhenLastModifiedIsUnchangedAndETagIsMissing()
    {
        // Arrange
        var lastModified = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        var remoteMetadata = new RegionUpdateMetadata(null, lastModified, 128);
        var storedMetadata = new RegionUpdateMetadata(null, lastModified, 42);
        var stateStore = new InMemoryRegionDownloadStateStore(
            new RegionDownloadState(Berlin.Id, Berlin.PbfUrl, storedMetadata, DateTimeOffset.UtcNow));
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(remoteMetadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        var result = await sut.CheckAsync(Berlin, "state.json");

        // Assert
        result.ShouldRedownloadPbf.Should().BeFalse("Last-Modified is the fallback when ETag is unavailable");
    }

    [Fact]
    public async Task CheckAsync_ShouldRedownloadPbf_WhenNoReliableRemoteMetadataExists()
    {
        // Arrange
        var remoteMetadata = new RegionUpdateMetadata(null, null, 128);
        var storedMetadata = new RegionUpdateMetadata(null, null, 128);
        var stateStore = new InMemoryRegionDownloadStateStore(
            new RegionDownloadState(Berlin.Id, Berlin.PbfUrl, storedMetadata, DateTimeOffset.UtcNow));
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(remoteMetadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        var result = await sut.CheckAsync(Berlin, "state.json");

        // Assert
        result.ShouldRedownloadPbf.Should().BeTrue();
        result.Reason.Should().Be("Remote metadata has no ETag or Last-Modified value.");
    }

    [Fact]
    public async Task MarkDownloadedAsync_WritesCurrentMetadataToState()
    {
        // Arrange
        var metadata = new RegionUpdateMetadata("\"abc\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        var stateStore = new InMemoryRegionDownloadStateStore();
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(metadata), Substitute.For<IRegionRenderStateStore>(), stateStore);

        // Act
        await sut.MarkDownloadedAsync(Berlin, "state.json", metadata);

        // Assert
        stateStore.WrittenState.Should().NotBeNull();
        stateStore.WrittenState!.RegionId.Should().Be(Berlin.Id);
        stateStore.WrittenState.PbfUrl.Should().Be(Berlin.PbfUrl);
        stateStore.WrittenState.DownloadedMetadata.Should().Be(metadata);
        stateStore.WrittenPath.Should().Be("state.json");
    }

    [Fact]
    public async Task MarkProcessedAsync_WritesRenderStateWithoutWritingDownloadState()
    {
        var metadata = new RegionUpdateMetadata("etag", null, 42);
        var renderStore = Substitute.For<IRegionRenderStateStore>();
        var downloadStore = Substitute.For<IRegionDownloadStateStore>();
        var sut = new RegionUpdateChecker(new StubRemotePbfMetadataReader(metadata), renderStore, downloadStore);

        await sut.MarkProcessedAsync(Berlin, "render-state.json", metadata);

        await renderStore.Received(1).WriteAsync(
            "render-state.json",
            Arg.Is<RegionRenderState>(state => state.LastProcessedMetadata == metadata && state.RegionId == Berlin.Id),
            Arg.Any<CancellationToken>());
        await downloadStore.DidNotReceiveWithAnyArgs().WriteAsync(default!, default!, default);
    }

    private sealed class StubRemotePbfMetadataReader : IRemotePbfMetadataReader
    {
        private readonly RegionUpdateMetadata _metadata;

        public StubRemotePbfMetadataReader(RegionUpdateMetadata metadata)
            => _metadata = metadata;

        public Task<RegionUpdateMetadata> GetMetadataAsync(
            string pbfUrl,
            CancellationToken ct = default)
            => Task.FromResult(_metadata);
    }

    private sealed class InMemoryRegionDownloadStateStore : IRegionDownloadStateStore
    {
        private readonly RegionDownloadState? _state;

        public InMemoryRegionDownloadStateStore(RegionDownloadState? state = null)
            => _state = state;

        public string? WrittenPath { get; private set; }
        public RegionDownloadState? WrittenState { get; private set; }

        public Task<RegionDownloadState?> ReadAsync(
            string statePath,
            CancellationToken ct = default)
            => Task.FromResult(_state);

        public Task DeleteAsync(string statePath, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task WriteAsync(
            string statePath,
            RegionDownloadState state,
            CancellationToken ct = default)
        {
            WrittenPath = statePath;
            WrittenState = state;
            return Task.CompletedTask;
        }
    }
}
