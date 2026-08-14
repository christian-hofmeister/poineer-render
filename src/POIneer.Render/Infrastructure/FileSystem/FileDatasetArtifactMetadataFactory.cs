using System.Security.Cryptography;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

// Default IDatasetArtifactMetadataFactory: reads basic filesystem facts (file name, size,
// last-write timestamp) and computes a SHA-256 checksum of an already-validated,
// already-canonical dataset artifact. CreatedUtc is derived from the artifact file's
// LastWriteTimeUtc rather than DateTimeOffset.UtcNow, so it stays deterministic and
// reflects when the dataset file was actually produced/promoted, not when this factory
// happened to run. Deliberately has no dependency on PublisherOptions or IDatasetPublisher - the
// metadata describes the artifact itself, not a publish destination (issue #130), so it
// stays usable unchanged regardless of which publisher implementation(s) exist (local
// #132, Azure #134, ...).
public sealed class FileDatasetArtifactMetadataFactory : IDatasetArtifactMetadataFactory
{
    private const int CopyBufferSize = 81920;

    public async Task<DatasetArtifactMetadata> CreateAsync(
        string regionId,
        string version,
        string artifactPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);

        if (!File.Exists(artifactPath))
        {
            throw new FileNotFoundException(
                $"Dataset artifact not found: {artifactPath}", artifactPath);
        }

        var fileInfo = new FileInfo(artifactPath);
        var checksum = await ComputeSha256Async(artifactPath, cancellationToken);

        return new DatasetArtifactMetadata(
            RegionId: regionId,
            Version: version,
            FileName: fileInfo.Name,
            FileSizeBytes: fileInfo.Length,
            CreatedUtc: fileInfo.LastWriteTimeUtc,
            Sha256Checksum: checksum);
    }

    // Streams the file rather than reading it fully into memory - datasets can be sizeable
    // regional SQLite databases, and this mirrors the streaming approach already used by
    // FileHashDatasetVersionCalculator for the same reason.
    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);

        var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
