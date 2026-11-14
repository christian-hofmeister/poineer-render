namespace POIneer.Render.Application.Contracts;

public sealed record RegionScheduleState(
    string RegionId,
    string PbfUrl,
    DateTimeOffset? LastSuccessfulCheckUtc,
    DateTimeOffset? NextDueUtc,
    bool ProcessingPending);
