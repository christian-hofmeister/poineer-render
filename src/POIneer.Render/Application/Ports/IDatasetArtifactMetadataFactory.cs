using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IDatasetArtifactMetadataFactory
{
    // Builds the DatasetArtifactMetadata for an already-validated, already-canonical
    // dataset artifact. Deliberately independent from IDatasetPublisher/PublisherOptions -
    // this describes the artifact itself, not where (or whether) it gets published
    // (issue #130), so it stays usable unchanged regardless of which publisher
    // implementation(s) exist.
    Task<DatasetArtifactMetadata> CreateAsync(
        string regionId,
        string version,
        string artifactPath,
        CancellationToken cancellationToken = default);
}
