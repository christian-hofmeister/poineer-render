using Microsoft.Data.Sqlite;
using POIneer.Render.Domain;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure.Sqlite;

/// <summary>
/// A repository implementation for managing Points of Interest (POIs) using SQLite as the underlying data store.
/// </summary>
public sealed class SqlitePoiRepository : IPoiRepository
{
    private const int IdOrdinal = 0;
    private const int OsmIdOrdinal = 1;
    private const int NameOrdinal = 2;
    private const int AmenityOrdinal = 3;
    private const int LatitudeOrdinal = 4;
    private const int LongitudeOrdinal = 5;

    private readonly Func<SqliteConnection> _connectionFactory;

    public SqlitePoiRepository(Func<SqliteConnection> connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task AddAsync(
        Poi poi,
        CancellationToken ct = default)
    {
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken: ct);

        var sql = $"""
            INSERT INTO {PoiTable.Name} (
                {PoiTable.OsmId}, 
                {PoiTable.NameColumn}, 
                {PoiTable.Amenity}, 
                {PoiTable.Latitude}, 
                {PoiTable.Longitude})
            VALUES (@osm_id, @name, @amenity, @latitude, @longitude);
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@osm_id", poi.OsmId);
        command.Parameters.AddWithValue("@name", (object?)poi.Name ?? DBNull.Value);
        command.Parameters.AddWithValue("@amenity", (object?)poi.Amenity ?? DBNull.Value);
        command.Parameters.AddWithValue("@latitude", poi.Location.Latitude);
        command.Parameters.AddWithValue("@longitude", poi.Location.Longitude);

        await command.ExecuteNonQueryAsync(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<Poi>> GetAllAsync(
        int limit = 100,
        CancellationToken ct = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        var result = new List<Poi>();

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken: ct);

        var sql = $"""
            SELECT 
                {PoiTable.Id}, 
                {PoiTable.OsmId}, 
                {PoiTable.NameColumn}, 
                {PoiTable.Amenity}, 
                {PoiTable.Latitude}, 
                {PoiTable.Longitude}
            FROM 
                {PoiTable.Name}
            ORDER BY 
                {PoiTable.Id}
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken: ct);

        while (await reader.ReadAsync(cancellationToken: ct))
        {
            var poi = MapPoi(reader);
            result.Add(poi);
        }

        return result;
    }

    public async Task<IReadOnlyList<Poi>> GetByAmenityAsync(string amenity, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(amenity))
        {
            throw new ArgumentException("Amenity must be a non-empty string.", nameof(amenity));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }
        var result = new List<Poi>();

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken: ct);

        var sql = $"""
            SELECT 
                {PoiTable.Id}, 
                {PoiTable.OsmId}, 
                {PoiTable.NameColumn}, 
                {PoiTable.Amenity}, 
                {PoiTable.Latitude}, 
                {PoiTable.Longitude}
            FROM 
                {PoiTable.Name}
            WHERE
                {PoiTable.Amenity} = @amenity
            ORDER BY 
                {PoiTable.Id}
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@amenity", amenity);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken: ct);

        while (await reader.ReadAsync(cancellationToken: ct))
        {
            var poi = MapPoi(reader);
            result.Add(poi);
        }

        return result;
    }

    public async Task<IReadOnlyList<Poi>> GetByNameAsync(string name, int limit = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name must be a non-empty string.", nameof(name));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        var result = new List<Poi>();

        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken: ct);

        var sql = $"""
            SELECT 
                {PoiTable.Id}, 
                {PoiTable.OsmId}, 
                {PoiTable.NameColumn}, 
                {PoiTable.Amenity}, 
                {PoiTable.Latitude}, 
                {PoiTable.Longitude}
            FROM 
                {PoiTable.Name}
            WHERE
                {PoiTable.NameColumn} = @name
            ORDER BY 
                {PoiTable.Id}
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken: ct);

        while (await reader.ReadAsync(cancellationToken: ct))
        {
            var poi = MapPoi(reader);
            result.Add(poi);
        }
        return result;
    }

    public async Task<IReadOnlyList<Poi>> GetByBoundingBoxAsync(
        BoundingBox boundingBox,
        int limit = 100,
        CancellationToken ct = default)
    {
        if (boundingBox is null)
        {
            throw new ArgumentNullException(nameof(boundingBox));
        }

        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        if (boundingBox.NorthWest.Latitude < boundingBox.SouthEast.Latitude)
        {
            throw new ArgumentException("NorthWest latitude must be greater than or equal to SouthEast latitude.");
        }

        if (boundingBox.NorthWest.Longitude > boundingBox.SouthEast.Longitude)
        {
            throw new ArgumentException("NorthWest longitude must be less than or equal to SouthEast longitude.");
        }


        var result = new List<Poi>();
        await using var connection = _connectionFactory();
        await connection.OpenAsync(cancellationToken: ct);
        var sql = $"""
            SELECT 
                {PoiTable.Id}, 
                {PoiTable.OsmId}, 
                {PoiTable.NameColumn}, 
                {PoiTable.Amenity}, 
                {PoiTable.Latitude}, 
                {PoiTable.Longitude}
            FROM 
                {PoiTable.Name}
            WHERE
                {PoiTable.Latitude} <= @northWestLat AND
                {PoiTable.Latitude} >= @southEastLat AND
                {PoiTable.Longitude} >= @northWestLon AND
                {PoiTable.Longitude} <= @southEastLon
            ORDER BY 
                {PoiTable.Id}
            LIMIT @limit;
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@northWestLat", boundingBox.NorthWest.Latitude);
        command.Parameters.AddWithValue("@southEastLat", boundingBox.SouthEast.Latitude);
        command.Parameters.AddWithValue("@northWestLon", boundingBox.NorthWest.Longitude);
        command.Parameters.AddWithValue("@southEastLon", boundingBox.SouthEast.Longitude);
        command.Parameters.AddWithValue("@limit", limit);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken: ct);

        while (await reader.ReadAsync(cancellationToken: ct))
        {
            var poi = MapPoi(reader);
            result.Add(poi);
        }

        return result;
    }

    private static Poi MapPoi(SqliteDataReader reader)
    {
        return new Poi(
            Id: reader.GetInt64(IdOrdinal),
            OsmId: reader.GetInt64(OsmIdOrdinal),
            Name: reader.IsDBNull(NameOrdinal) ? null : reader.GetString(NameOrdinal),
            Amenity: reader.IsDBNull(AmenityOrdinal) ? null : reader.GetString(AmenityOrdinal),
            Location: new GeoPoint(
                Latitude: reader.GetDouble(LatitudeOrdinal),
                Longitude: reader.GetDouble(LongitudeOrdinal)));
    }
}