using POIneer.Render.Application.Contracts;

namespace POIneer.Render.Application.Ports;

public interface IPublishedDatasetVerifier
{
    // Verifies that the dataset artifact actually present at a publish destination matches
    // expectedMetadata - the DatasetArtifactMetadata already computed for the source
    // artifact before publishing (issue #130) - so a publish is only considered successful
    // once what landed at the destination is confirmed complete and byte-identical, not
    // just because the publish call itself returned without throwing (issue #135).
    // Deliberately takes the already-computed expectedMetadata rather than a source path, so
    // an implementation never re-reads or re-hashes the source artifact - only the
    // published one.
    Task<DatasetVerificationResult> VerifyAsync(
        DatasetArtifactMetadata expectedMetadata,
        string destinationPath,
        CancellationToken cancellationToken = default);
}
