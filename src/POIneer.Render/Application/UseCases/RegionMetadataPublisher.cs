using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Application.UseCases;

public sealed class RegionMetadataPublisher(IRegionMetadataStore store, TimeProvider clock)
{
    private const string CatalogKey = "regions.json";
    private static readonly JsonSerializerOptions CatalogJson = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions ManifestJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private static readonly Regex VersionPattern = new(@"\A[1-9][0-9]*-[a-f0-9]{16}\z", RegexOptions.NonBacktracking);
    private static readonly Regex ChecksumPattern = new(@"\A[a-f0-9]{64}\z", RegexOptions.NonBacktracking);

    public async Task PublishCatalogAsync(IReadOnlyList<RegionDto> regions, CancellationToken ct)
    {
        if (regions.Count == 0)
            throw new InvalidDataException("The region catalog source is empty.");

        var configured = ValidateCatalog(regions.Select(region =>
            new CatalogRegion(region.Id, region.Name, region.Country, region.Category, region.PbfUrl)));
        var current = await store.ReadAsync(CatalogKey, ct);
        if (current is not null)
        {
            var previous = JsonSerializer.Deserialize<CatalogRegion[]>(current.Content, CatalogJson)
                ?? throw new InvalidDataException("The published region catalog is empty.");
            foreach (var entry in ValidateCatalog(previous))
                configured.TryAdd(entry.Key, entry.Value);
        }

        var content = JsonSerializer.SerializeToUtf8Bytes(
            configured.Values.OrderBy(region => region.Id, StringComparer.Ordinal).ToArray(), CatalogJson);
        if (current is not null && content.AsSpan().SequenceEqual(current.Content))
            return;

        if (!await store.TryWriteAsync(CatalogKey, content, current?.Revision, ct))
            throw new IOException("The published region catalog changed during this run. Retry publication.");
    }

    public async Task<string?> ReadManifestRevisionAsync(string regionId, CancellationToken ct)
        => (await store.ReadAsync(ManifestKey(regionId), ct))?.Revision;

    public async Task<bool> TryPublishManifestAsync(
        string regionId, string releaseVersion, IReadOnlyList<RegionManifestArtifact> artifacts,
        string? expectedRevision, CancellationToken ct)
    {
        var key = ManifestKey(regionId);
        var manifest = new RegionManifest(1, regionId, releaseVersion, clock.GetUtcNow().UtcDateTime, artifacts);
        ValidateManifest(manifest);

        var current = await store.ReadAsync(key, ct);
        if (current is not null)
        {
            var previous = JsonSerializer.Deserialize<RegionManifest>(current.Content, ManifestJson)
                ?? throw new InvalidDataException($"The current manifest for '{regionId}' is empty.");
            ValidateManifest(previous);
            if (previous.RegionId != regionId)
                throw new InvalidDataException($"The current manifest does not belong to '{regionId}'.");
            if (previous.ReleaseVersion == releaseVersion)
            {
                if (!previous.Artifacts.OrderBy(artifact => artifact.Type, StringComparer.Ordinal)
                        .SequenceEqual(artifacts.OrderBy(artifact => artifact.Type, StringComparer.Ordinal)))
                    throw new InvalidDataException($"Release '{releaseVersion}' has conflicting artifacts for '{regionId}'.");
                return true;
            }
        }

        if (!string.Equals(current?.Revision, expectedRevision, StringComparison.Ordinal))
            return false;

        return await store.TryWriteAsync(
            key, JsonSerializer.SerializeToUtf8Bytes(manifest, ManifestJson), expectedRevision, ct);
    }

    private static string ManifestKey(string regionId)
    {
        RegionIdentifier.ValidateHierarchicalId(regionId, nameof(regionId));
        return $"{regionId}/manifest.json";
    }

    internal static void ValidateVersion(string version)
    {
        if (!VersionPattern.IsMatch(version))
            throw new InvalidDataException("The generated release version does not match the v1 manifest contract. Check Publisher:SchemaVersion.");
    }

    private static Dictionary<string, CatalogRegion> ValidateCatalog(IEnumerable<CatalogRegion> regions)
    {
        var entries = new Dictionary<string, CatalogRegion>(StringComparer.Ordinal);
        foreach (var region in regions)
        {
            if (region is null || string.IsNullOrWhiteSpace(region.Name))
                throw new InvalidDataException("Each catalog entry requires a display name.");
            RegionIdentifier.ValidateHierarchicalId(region.Id, nameof(region.Id));
            if (!string.IsNullOrWhiteSpace(region.PbfUrl)
                && (!Uri.TryCreate(region.PbfUrl, UriKind.Absolute, out var url)
                    || (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp)
                    || url.UserInfo.Length != 0 || url.Query.Length != 0 || url.Fragment.Length != 0))
                throw new InvalidDataException($"Catalog entry '{region.Id}' has a PBF URL that is not a public HTTP(S) URL without credentials, query or fragment.");
            if (!entries.TryAdd(region.Id, region))
                throw new InvalidDataException($"Duplicate region ID '{region.Id}' in the region catalog.");
        }
        return entries;
    }

    private static void ValidateManifest(RegionManifest manifest)
    {
        var leaf = RegionIdentifier.GetLeafSegment(manifest.RegionId);
        if (manifest.SchemaVersion != 1 || !VersionPattern.IsMatch(manifest.ReleaseVersion)
            || manifest.PublishedAt.Kind != DateTimeKind.Utc || manifest.Artifacts is null
            || manifest.Artifacts.Count is < 1 or > 2
            || manifest.Artifacts.Count(artifact => artifact?.Type == "sqlite") != 1)
            throw new InvalidDataException($"Invalid release manifest for '{manifest.RegionId}'.");

        foreach (var artifact in manifest.Artifacts)
        {
            if (artifact is null || artifact.Type is not ("sqlite" or "pmtiles")
                || !VersionPattern.IsMatch(artifact.ArtifactVersion)
                || artifact.SizeBytes <= 0 || !ChecksumPattern.IsMatch(artifact.Sha256)
                || artifact.ObjectKey != $"{manifest.RegionId}/{leaf}.{artifact.ArtifactVersion}.{artifact.Type}")
                throw new InvalidDataException($"Invalid artifact in the release manifest for '{manifest.RegionId}'.");
        }
    }

    private sealed record CatalogRegion(string Id, string Name, string? Country, string? Category, string? PbfUrl);
}
