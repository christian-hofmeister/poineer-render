using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IRegionDownloadStateStore
{
    Task<RegionDownloadState?> ReadAsync(
        string statePath,
        CancellationToken ct = default);

    Task DeleteAsync(string statePath, CancellationToken ct = default);

    Task WriteAsync(
        string statePath,
        RegionDownloadState state,
        CancellationToken ct = default);
}
