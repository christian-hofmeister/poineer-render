namespace POIneer.Render.Ports;

using POIneer.Render.Domain.Models;

public interface IExporter
{
    // Exports POIs to a SQLite database
    Task ExportAsync(
        IAsyncEnumerable<Poi> pois,
        string outputSqlitePath,
        CancellationToken ct = default);
}