namespace POIneer.Render.Application.Contracts;

public sealed record RegionManifest(
    int SchemaVersion,
    string RegionId,
    string ReleaseVersion,
    DateTime PublishedAt,
    IReadOnlyList<RegionManifestArtifact> Artifacts);

public sealed record RegionManifestArtifact(
    string Type,
    string ArtifactVersion,
    string ObjectKey,
    long SizeBytes,
    string Sha256);
