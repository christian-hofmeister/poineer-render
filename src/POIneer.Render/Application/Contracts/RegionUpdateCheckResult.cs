namespace POIneer.Render.Application.Contracts;

public sealed record RegionUpdateCheckResult(
    bool ShouldRender,
    string Reason,
    RegionUpdateMetadata RemoteMetadata,
    RegionRenderState? StoredState);
