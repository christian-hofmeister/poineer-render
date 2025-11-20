namespace POIneer.Render.Adapters.Output;

using System.Data.SQLite;
using POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;

public sealed class SqliteExporter : IExporter
{
    public async Task ExportAsync(
        IAsyncEnumerable<PoiDto> pois,
        string outputSqlitePath,
        CancellationToken ct = default)
    {
        if (File.Exists(outputSqlitePath)) File.Delete(outputSqlitePath);

        SQLiteConnection.CreateFile(outputSqlitePath);
        using var conn = new SQLiteConnection($"Data Source={outputSqlitePath};Journal Mode=WAL;");
        await conn.OpenAsync(ct);

        using var cmdCreate = conn.CreateCommand();
        cmdCreate.CommandText = """
            CREATE TABLE poi (
              id INTEGER PRIMARY KEY AUTOINCREMENT,
              osm_id INTEGER,
              name TEXT,
              category TEXT,
              lon REAL,
              lat REAL
            );
            """;
        await cmdCreate.ExecuteNonQueryAsync(ct);

        using var tx = conn.BeginTransaction();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO poi (osm_id,name,category,lon,lat) VALUES (@o,@n,@c,@x,@y)";
        var pO = cmd.CreateParameter(); pO.ParameterName = "@o"; cmd.Parameters.Add(pO);
        var pN = cmd.CreateParameter(); pN.ParameterName = "@n"; cmd.Parameters.Add(pN);
        var pC = cmd.CreateParameter(); pC.ParameterName = "@c"; cmd.Parameters.Add(pC);
        var pX = cmd.CreateParameter(); pX.ParameterName = "@x"; cmd.Parameters.Add(pX);
        var pY = cmd.CreateParameter(); pY.ParameterName = "@y"; cmd.Parameters.Add(pY);

        await foreach (var poi in pois.WithCancellation(ct))
        {
            pO.Value = poi.OsmId; pN.Value = poi.Name ?? (object)DBNull.Value;
            pC.Value = poi.Category; pX.Value = poi.Lon; pY.Value = poi.Lat;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        tx.Commit();
    }
}