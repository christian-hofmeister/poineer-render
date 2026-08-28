using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Options;

// Strongly-typed configuration bound from the "Publisher" configuration section.
public sealed class PublisherOptions
{
    // Directory validated datasets are published to. Configurable per environment so the
    // same LocalDatasetPublisher works for a local development folder or a filesystem
    // location available on the VPS - never hardcode a machine-specific path in code.
    public required string DestinationDir { get; init; }

    public DatasetPublishOverwritePolicy OverwritePolicy { get; init; } = DatasetPublishOverwritePolicy.Skip;

    // Bumped deliberately (as part of a deployment) whenever a new POIneer.Render release
    // changes the exported schema or POI mapping in a way that should be republished even
    // though the source OSM PBF is unchanged. Combined with a hash of the source PBF
    // content to form the dataset version - see IDatasetVersionCalculator.
    public string SchemaVersion { get; init; } = "2";
}
