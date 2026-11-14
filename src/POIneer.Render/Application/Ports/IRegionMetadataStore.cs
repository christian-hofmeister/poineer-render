namespace POIneer.Render.Application.Ports;

public sealed record StoredRegionMetadata(string Revision, byte[] Content);

public interface IRegionMetadataStore
{
    Task<StoredRegionMetadata?> ReadAsync(string key, CancellationToken ct);

    // A null revision means the destination must not exist.
    Task<bool> TryWriteAsync(string key, byte[] content, string? expectedRevision, CancellationToken ct);
}
