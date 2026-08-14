namespace POIneer.Render.Application.Contracts;

// Outcome of verifying a published dataset artifact against the metadata computed for the
// source artifact before publishing (issue #135). IsVerified is true only when the
// published artifact exists at its destination and its file size and checksum both match
// the expected DatasetArtifactMetadata exactly.
public sealed record DatasetVerificationResult(
    bool IsVerified,
    IReadOnlyList<string> Errors);
