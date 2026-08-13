namespace POIneer.Render.Application.Contracts;

// Identifies a validated dataset artifact ready to be published, together with the region
// and version identifiers a publish destination needs to preserve (issue #132).
public sealed record DatasetPublishRequest(
    string RegionId,
    string Version,
    string SourcePath);
