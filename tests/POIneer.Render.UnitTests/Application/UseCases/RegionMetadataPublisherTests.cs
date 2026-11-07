using System.Text.Json;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases;

public sealed class RegionMetadataPublisherTests
{
    private const string RegionId = "geofabrik/europe/germany/berlin";

    [Fact]
    public async Task TryPublishManifestAsync_PreservesCurrentRelease_WhenWriterHasStaleRevision()
    {
        await using var temp = TestTemporaryDirectories.Create("manifest-revision");
        var store = new FileRegionMetadataStore(temp.DirectoryPath);
        var publisher = new RegionMetadataPublisher(store, TimeProvider.System);
        var first = Artifact("2-aaaaaaaaaaaaaaaa");
        var second = Artifact("2-bbbbbbbbbbbbbbbb");
        var third = Artifact("2-cccccccccccccccc");
        var ct = CancellationToken.None;

        Assert.True(await publisher.TryPublishManifestAsync(RegionId, first.ArtifactVersion, [first], null, ct));
        var previousRevision = await publisher.ReadManifestRevisionAsync(RegionId, ct);
        Assert.True(await publisher.TryPublishManifestAsync(RegionId, second.ArtifactVersion, [second], previousRevision, ct));
        var currentRevision = await publisher.ReadManifestRevisionAsync(RegionId, ct);

        Assert.False(await publisher.TryPublishManifestAsync(RegionId, third.ArtifactVersion, [third], previousRevision, ct));
        Assert.True(await publisher.TryPublishManifestAsync(RegionId, second.ArtifactVersion, [second], previousRevision, ct));
        Assert.Equal(currentRevision, await publisher.ReadManifestRevisionAsync(RegionId, ct));
    }

    [Fact]
    public async Task PublishCatalogAsync_PreservesPriorEntries_AndRejectsInvalidReplacement()
    {
        await using var temp = TestTemporaryDirectories.Create("metadata-catalog");
        var store = new FileRegionMetadataStore(temp.DirectoryPath);
        var publisher = new RegionMetadataPublisher(store, TimeProvider.System);
        var berlin = new RegionDto(RegionId, "Berlin", "https://example.com/berlin.pbf", "/private/runtime/berlin.poly");
        var hamburg = new RegionDto("geofabrik/europe/germany/hamburg", "Hamburg", "https://example.com/hamburg.pbf", null);
        var ct = CancellationToken.None;
        await publisher.PublishCatalogAsync([berlin], ct);
        await publisher.PublishCatalogAsync([hamburg], ct);
        var snapshot = await store.ReadAsync("regions.json", ct);
        Assert.NotNull(snapshot);
        using var catalog = JsonDocument.Parse(snapshot.Content);
        Assert.Equal(2, catalog.RootElement.GetArrayLength());
        Assert.Equal(RegionId, catalog.RootElement[0].GetProperty("Id").GetString());
        Assert.False(catalog.RootElement[0].TryGetProperty("Poly", out _));

        await Assert.ThrowsAsync<InvalidDataException>(() => publisher.PublishCatalogAsync([berlin, berlin], ct));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            publisher.PublishCatalogAsync([berlin with { PbfUrl = "https://example.com/berlin.pbf?sig=placeholder" }], ct));
        Assert.Equal(snapshot.Revision, (await store.ReadAsync("regions.json", ct))?.Revision);
    }

    private static RegionManifestArtifact Artifact(string version)
        => new("sqlite", version, $"{RegionId}/berlin.{version}.sqlite", 1, new string('a', 64));
}
