using System.Drawing;
using Microsoft.Data.Sqlite;
using POIneer.Render.Domain;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure.Sqlite;

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
    private const int IdOrdinal = 0;
    private const int OsmIdOrdinal = 1;
    private const int NameOrdinal = 2;
    private const int AmenityOrdinal = 3;
    private const int LatitudeOrdinal = 4;
    private const int LongitudeOrdinal = 5;

    private readonly Func<SqliteConnection> _connectionFactory;

    // Constructor that accepts a factory function to create SQLite connections.
    public SqlitePoiRepository(Func<SqliteConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    // Adds a new POI to the SQLite database.
    public async Task AddAsync(
        Poi poi,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync();

        const string sql = @"
INSERT INTO poi (id, osm_id, name, amenity, latitude, longitude)
VALUES (@id, @osm_id, @name, @amenity, @latitude, @longitude);
";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@id", poi.Id);
        command.Parameters.AddWithValue("@osm_id", poi.OsmId);
        command.Parameters.AddWithValue("@name", poi.Name);
        command.Parameters.AddWithValue("@amenity", poi.Amenity);
        command.Parameters.AddWithValue("@latitude", poi.Location.Latitude);
        command.Parameters.AddWithValue("@longitude", poi.Location.Longitude);

        await command.ExecuteNonQueryAsync();
    }

    // Retrieves all POIs from the SQLite database up to the specified limit.
    public async Task<IReadOnlyList<Poi>> GetAllAsync(
        int limit = 100,
        CancellationToken ct = default)
    {
        var result = new List<Poi>();

        await using var connection = _connectionFactory();
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
                Id: reader.GetInt64(IdOrdinal),
                OsmId: reader.GetInt64(OsmIdOrdinal),
                Name: reader.IsDBNull(NameOrdinal) ? null : reader.GetString(NameOrdinal),
                Amenity: reader.IsDBNull(AmenityOrdinal) ? null : reader.GetString(AmenityOrdinal),
                Location: new GeoPoint(
                    Latitude: reader.GetDouble(LatitudeOrdinal),
                    Longitude: reader.GetDouble(LongitudeOrdinal)
                )
            );

            result.Add(poi);
        }
        return result;
    }
}
