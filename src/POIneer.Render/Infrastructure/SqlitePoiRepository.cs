using Microsoft.Data.Sqlite;
using POIneer.Render.Domain;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure;

// Repository implementation that reads from a SQLite database file
/// to retrieve Point of Interest (POI) data.
/// /// <remarks>
/// Assumes the SQLite database has a table named 'poi' with columns:
/// - id (INTEGER PRIMARY KEY)
/// - osm_id (TEXT)
/// - name (TEXT)
/// - amenity (TEXT)        
/// - latitude (REAL)
/// - longitude (REAL)
/// </remarks>
public sealed class SqlitePoiRepository : IPoiRepository
{
    private readonly string _connectionString;

    public SqlitePoiRepository(string databasePath)
    {
        // Example: "output/berlin_v1.sqlite"
        _connectionString = $"Data Source={databasePath}";
    }

    // Retrieves all POIs from the SQLite database up to the specified limit.
    public async Task<IReadOnlyList<Poi>> GetAllAsync(int limit = 100)
    {
        var result = new List<Poi>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // SQL query to select POI data
        const string sql = @"
SELECT id, osm_id, name, amenity, latitude, longitude
FROM poi
ORDER BY id
LIMIT @limit;
";

        // Prepare and execute the SQL command
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@limit", limit);

        // Execute the SQL command and read the results
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var poi = new Poi(
                Id: reader.GetInt64(0),
                OsmId: reader.GetString(1),
                Name: reader.IsDBNull(2) ? null : reader.GetString(2),
                Amenity: reader.GetString(3),
                Latitude: reader.GetDouble(4),
                Longitude: reader.GetDouble(5)
            );

            result.Add(poi);
        }

        return result;
    }
}
