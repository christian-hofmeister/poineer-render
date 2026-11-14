using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Application.UseCases;

public sealed class RegionUpdateChecker : IRegionUpdateChecker
{
    private readonly IRemotePbfMetadataReader _metadataReader;
    private readonly IRegionRenderStateStore _stateStore;
    private readonly IRegionDownloadStateStore _downloadStateStore;

    public RegionUpdateChecker(
        IRemotePbfMetadataReader metadataReader,
        IRegionRenderStateStore stateStore,
        IRegionDownloadStateStore downloadStateStore)
    {
        _metadataReader = metadataReader;
        _stateStore = stateStore;
        _downloadStateStore = downloadStateStore;
    }

    public async Task<RegionUpdateCheckResult> CheckAsync(
        RegionDto region,
        string statePath,
        CancellationToken ct = default)
    {
        var remoteMetadata = await _metadataReader.GetMetadataAsync(region.PbfUrl, ct);
        var storedState = await _downloadStateStore.ReadAsync(statePath, ct);
        var reason = GetChangeReason(region, remoteMetadata, storedState);

        return new RegionUpdateCheckResult(
            ShouldRedownloadPbf: reason is not null,
            Reason: reason ?? "Remote PBF metadata is unchanged.",
            RemoteMetadata: remoteMetadata,
            StoredState: storedState);
    }

    public Task InvalidateDownloadAsync(string statePath, CancellationToken ct = default)
        => _downloadStateStore.DeleteAsync(statePath, ct);

    public async Task<bool> MatchesProcessedChecksumAsync(
        RegionDto region, string statePath, string checksum, CancellationToken ct = default)
    {
        var state = await _stateStore.ReadAsync(statePath, ct);
        return state is not null
            && state.RegionId == region.Id
            && state.PbfUrl == region.PbfUrl
            && StringComparer.OrdinalIgnoreCase.Equals(checksum, state.LastProcessedMetadata.Md5Checksum);
    }

    public Task MarkDownloadedAsync(
        RegionDto region,
        string statePath,
        RegionUpdateMetadata metadata,
        CancellationToken ct = default)
        => _downloadStateStore.WriteAsync(
            statePath,
            new RegionDownloadState(region.Id, region.PbfUrl, metadata, DateTimeOffset.UtcNow),
            ct);

    public Task MarkProcessedAsync(
        RegionDto region,
        string statePath,
        RegionUpdateMetadata metadata,
        CancellationToken ct = default)
    {
        var state = new RegionRenderState(
            region.Id,
            region.PbfUrl,
            metadata,
            DateTimeOffset.UtcNow);

        return _stateStore.WriteAsync(statePath, state, ct);
    }

    private static string? GetChangeReason(
        RegionDto region,
        RegionUpdateMetadata remoteMetadata,
        RegionDownloadState? storedState)
    {
        if (storedState is null)
            return "No previous download state exists for this region.";

        if (!StringComparer.Ordinal.Equals(storedState.RegionId, region.Id))
            return "Stored download state belongs to a different region.";

        if (!StringComparer.Ordinal.Equals(storedState.PbfUrl, region.PbfUrl))
            return "Configured PBF URL changed.";

        if (remoteMetadata.Md5Checksum is { } checksum)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(checksum, storedState.DownloadedMetadata.Md5Checksum)
                ? null
                : "Geofabrik MD5 checksum changed or no verified download checksum exists.";
        }

        if (!string.IsNullOrWhiteSpace(remoteMetadata.ETag))
        {
            return StringComparer.Ordinal.Equals(remoteMetadata.ETag, storedState.DownloadedMetadata.ETag)
                ? null
                : "Remote ETag changed.";
        }

        if (remoteMetadata.LastModified.HasValue)
        {
            return remoteMetadata.LastModified == storedState.DownloadedMetadata.LastModified
                ? null
                : "Remote Last-Modified changed.";
        }

        return "Remote metadata has no ETag or Last-Modified value.";
    }
}
