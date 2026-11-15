using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace POIneer.Render.Application.UseCases;

public sealed class PendingManifestStore(ILogger<PendingManifestStore> logger)
{
    internal bool Exists(string regionOutDir) => File.Exists(GetPath(regionOutDir));

    internal void Delete(string regionOutDir) => File.Delete(GetPath(regionOutDir));

    internal async Task<PendingManifest?> ReadAsync(string regionOutDir, CancellationToken ct)
    {
        var path = GetPath(regionOutDir);
        try
        {
            return JsonSerializer.Deserialize<PendingManifest>(await File.ReadAllBytesAsync(path, ct));
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Ignoring invalid manifest checkpoint at {Path}.", path);
            return null;
        }
    }

    internal async Task WriteAsync(string regionOutDir, PendingManifest pending, CancellationToken ct)
    {
        var path = GetPath(regionOutDir);
        var stagingPath = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(stagingPath, JsonSerializer.SerializeToUtf8Bytes(pending), ct);
            File.Move(stagingPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(stagingPath);
        }
    }

    internal async Task<string> CalculateSourceSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    private static string GetPath(string regionOutDir) => Path.Combine(regionOutDir, "manifest-pending.json");
}

internal sealed record PendingManifest(string ReleaseVersion, string? ExpectedRevision, string? SourceSha256);
