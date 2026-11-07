using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Options;

public sealed class MetadataPublisherOptions
{
    public const string SectionName = "MetadataPublisher";

    public DatasetPublisherTarget? Target { get; init; }
    public string? DestinationDir { get; init; }
    public string? BlobEndpoint { get; init; }
    public string? AccountName { get; init; }
    public string? ContainerName { get; init; }
    public string? Prefix { get; init; }
}
