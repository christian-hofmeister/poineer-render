namespace POIneer.Render.Application.Ports;

public interface IDatasetVersionCalculator
{
    // Computes a shared release version from all completed artifacts and SchemaVersion.
    // Byte-identical artifact sets produce the same version, independent of their paths or order.
    Task<string> CalculateAsync(IReadOnlyCollection<string> artifactPaths, CancellationToken cancellationToken = default);
}
