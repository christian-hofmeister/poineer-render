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
}
