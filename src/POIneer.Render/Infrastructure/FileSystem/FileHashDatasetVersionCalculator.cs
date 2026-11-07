using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

// Hashes artifact types and their content hashes in a stable order to identify the whole release.
public sealed class FileHashDatasetVersionCalculator : IDatasetVersionCalculator
{
    private const int HashPrefixLength = 16;
    private const int CopyBufferSize = 81920;
    private readonly PublisherOptions _options;

    public FileHashDatasetVersionCalculator(IOptions<PublisherOptions> options)
        => _options = options.Value;

    public async Task<string> CalculateAsync(
        IReadOnlyCollection<string> artifactPaths,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifactPaths);
        if (artifactPaths.Count == 0)
            throw new ArgumentException("At least one artifact is required.", nameof(artifactPaths));

        var artifacts = artifactPaths.Select(path =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            return (Path: path, Type: Path.GetExtension(path).ToLowerInvariant());
        }).OrderBy(artifact => artifact.Type, StringComparer.Ordinal).ToArray();

        if (artifacts.Select(artifact => artifact.Type).Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
            throw new ArgumentException("Only one artifact per file type is supported.", nameof(artifactPaths));

        using var releaseHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var artifact in artifacts)
        {
            await using var stream = new FileStream(
                artifact.Path, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true);
            var contentHash = await SHA256.HashDataAsync(stream, cancellationToken);
            // Newline-delimited records contain a normalized type and a fixed-size hex hash.
            releaseHash.AppendData(Encoding.UTF8.GetBytes($"{artifact.Type}:{Convert.ToHexString(contentHash)}\n"));
        }

        var hashPrefix = Convert.ToHexString(releaseHash.GetHashAndReset())[..HashPrefixLength].ToLowerInvariant();
        return $"{_options.SchemaVersion}-{hashPrefix}";
    }
}
