using System.Security.Cryptography;
using POIneer.Render.Application.Ports;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class FileRegionMetadataStore(string root) : IRegionMetadataStore
{
    public async Task<StoredRegionMetadata?> ReadAsync(string key, CancellationToken ct)
    {
        var path = RegionIdentifier.CombinePath(root, key);
        try
        {
            var content = await File.ReadAllBytesAsync(path, ct);
            return new StoredRegionMetadata(Convert.ToHexString(SHA256.HashData(content)), content);
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public async Task<bool> TryWriteAsync(string key, byte[] content, string? expectedRevision, CancellationToken ct)
    {
        var path = RegionIdentifier.CombinePath(root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Keep the lock file in place so every writer coordinates through the same file.
        await using var writeLock = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        var current = await ReadAsync(key, ct);
        if (!string.Equals(current?.Revision, expectedRevision, StringComparison.Ordinal))
            return false;

        var stagingPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(stagingPath, content, ct);
            ct.ThrowIfCancellationRequested();
            File.Move(stagingPath, path, overwrite: expectedRevision is not null);
            return true;
        }
        finally
        {
            File.Delete(stagingPath);
        }
    }
}
