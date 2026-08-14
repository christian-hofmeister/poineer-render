namespace POIneer.Render.Application.Contracts;

// Basic technical metadata describing a generated dataset artifact, independent from any
// specific publishing target such as Azure Blob Storage (issue #130). Serves as a stable
// foundation the surrounding dataset-publishing epic (#131-#137) can build on for
// publishing, versioning, integrity checks, and eventually client downloads.
public sealed record DatasetArtifactMetadata(
    string RegionId,
    string Version,
    string FileName,
    long FileSizeBytes,
    DateTimeOffset CreatedUtc,
    string Sha256Checksum);
