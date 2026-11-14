namespace POIneer.Render.Ports;

using POIneer.Render.Application.Contracts;

public sealed record RegionPublicationContext(string? ExpectedManifestRevision);

public interface IRenderRegion
{
    Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default,
        bool forceRebuild = false);

    Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        RegionPublicationContext publication,
        CancellationToken ct,
        bool forceRebuild);
}
