namespace POIneer.Render.Application.Contracts;

public sealed record RegionDownloadState(
    string RegionId,
    string PbfUrl,
    RegionUpdateMetadata DownloadedMetadata,
    DateTimeOffset DownloadedAtUtc);
