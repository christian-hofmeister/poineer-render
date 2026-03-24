using System.Diagnostics;
using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite;

public sealed class SqlitePoiRepositoryTests
{
    [Fact]
    public async Task AddAndList_ReturnsInsertedPoi()
    {
        // Arrange
        //if (!IsFlywayAvailable())
        //    return;
        await using var temp = TemporaryDirectory.Create("poineer-flyway-it-", true);
        var root = temp.DirectoryPath;
        var dbPath = Path.Combine(root, $"{Guid.NewGuid():N}.sqlite");
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        await using (var con = new SqliteConnection(cs))
        {
            await con.OpenAsync();

            // Minimal schema for happy path - adapt to your real table/columns
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE poi (
                    id              INTEGER PRIMARY KEY,
                    osm_id          TEXT NOT NULL,
                    name            TEXT,
                    amenity         TEXT,
                    latitude        REAL NOT NULL,
                    longitude       REAL NOT NULL
                );
               """;
            await cmd.ExecuteNonQueryAsync();
        }

        // TODO: create your repository with the same connection string
        // e.g. var sut = new SqlitePoiRepository(cs);
        var sut = CreateSut(dbPath);

        var poiId = 1234L;

        // Act
        await sut.AddAsync(new Poi(
            Id: poiId,
            OsmId: "osm123",
            Name: "Test POI",
            Amenity: "cafe",
            Latitude: 52.5200,
            Longitude: 13.4050
        ), CancellationToken.None);

        var all = (await sut.GetAllAsync(100, CancellationToken.None)).ToList();


        // Assert
        Assert.Contains(all, p => p.Id == poiId && p.Name == "Test POI");

        // Cleanup
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    private static SqlitePoiRepository CreateSut(string dbPath) => new SqlitePoiRepository(() =>
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        return new SqliteConnection(cs);
    });

    //private sealed record Poi(string Id, string Name, double Lat, double Lon); 

    private static bool IsFlywayAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "flyway",
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
