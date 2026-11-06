using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IRegionScheduleStateStore
{
    Task<RegionScheduleState?> ReadAsync(string statePath, CancellationToken ct = default);
    Task WriteAsync(string statePath, RegionScheduleState state, CancellationToken ct = default);
}
