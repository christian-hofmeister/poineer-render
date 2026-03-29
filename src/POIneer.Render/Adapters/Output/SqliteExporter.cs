using Microsoft.Data.Sqlite;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;

namespace POIneer.Render.Adapters.Output;

public sealed class SqliteExporter : IExporter
{
    public async Task ExportAsync(
        IAsyncEnumerable<PoiDto> pois,
        string outputSqlitePath,
        CancellationToken ct = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = outputSqlitePath
        }.ToString();

        await using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);

        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var cmd = conn.CreateCommand();

        cmd.Transaction = (SqliteTransaction?)tx;
        cmd.CommandText = """
            INSERT INTO poi (osm_id, name, amenity, latitude, longitude)
            VALUES (@osm_id, @name, @amenity, @latitude, @longitude);
            """;

        cmd.Parameters.AddWithValue("@osm_id", "");
        cmd.Parameters.AddWithValue("@name", "");
        cmd.Parameters.AddWithValue("@amenity", "");
        cmd.Parameters.AddWithValue("@latitude", 0d);
        cmd.Parameters.AddWithValue("@longitude", 0d);

        await foreach (var poi in pois.WithCancellation(ct))
        {
            cmd.Parameters["@osm_id"].Value = poi.OsmId;
            cmd.Parameters["@name"].Value = poi.Name ?? (object)DBNull.Value;
            cmd.Parameters["@amenity"].Value = poi.Amenity ?? (object)DBNull.Value;
            cmd.Parameters["@latitude"].Value = poi.Latitude;
            cmd.Parameters["@longitude"].Value = poi.Longitude;

            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }
}