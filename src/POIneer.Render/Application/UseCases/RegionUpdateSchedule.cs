using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Application.UseCases;

public sealed class RegionUpdateSchedule(
    IRegionScheduleStateStore store,
    RegionUpdateIntervalPolicy intervalPolicy,
    TimeProvider timeProvider)
{
    public async Task<RegionScheduleState?> ReadAsync(RegionDto region, string path, CancellationToken ct)
    {
        var state = await store.ReadAsync(path, ct);
        return state is not null && state.RegionId == region.Id && state.PbfUrl == region.PbfUrl
            ? state : null;
    }

    public bool IsDue(RegionScheduleState? state)
        => state is null || state.ProcessingPending || state.NextDueUtc is null
           || timeProvider.GetUtcNow() >= state.NextDueUtc;

    public Task MarkProcessingAsync(RegionDto region, string path, RegionScheduleState? previous, CancellationToken ct)
        => store.WriteAsync(path, new RegionScheduleState(region.Id, region.PbfUrl,
            previous?.LastSuccessfulCheckUtc, previous?.NextDueUtc, ProcessingPending: true), ct);

    public async Task<RegionScheduleState> MarkSuccessfulAsync(RegionDto region, string path, CancellationToken ct)
    {
        var completedUtc = timeProvider.GetUtcNow();
        var state = new RegionScheduleState(region.Id, region.PbfUrl, completedUtc,
            completedUtc.AddDays(intervalPolicy.GetIntervalDays(region)), ProcessingPending: false);
        await store.WriteAsync(path, state, ct);
        return state;
    }
}
