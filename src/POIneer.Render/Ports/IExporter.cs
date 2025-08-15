namespace POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;

public interface IExporter
{
    // Exports POIs to a SQLite database
    Task ExportAsync(
        IAsyncEnumerable<PoiDto> pois,
        string outputSqlitePath,
        CancellationToken ct = default);
}