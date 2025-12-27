using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public sealed class SqlitePoiRepositoryTests
{
    // var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    // Directory.CreateDirectory(tempDir);

    /*    [Fact]
       public async Task AddAndList_ReturnsInsertedPoi()
       {
           // Arrange
           var dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sqlite");
           var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

           await using (var con = new SqliteConnection(cs))
           {
               await con.OpenAsync();

               // Minimal schema for happy path - adapt to your real table/columns
               var cmd = con.CreateCommand();
               cmd.CommandText = """
                   CREATE TABLE poi (
                       id TEXT PRIMARY KEY,
                       name TEXT NOT NULL,
                       lat REAL NOT NULL,
                       lon REAL NOT NULL
                   );
               """;
               await cmd.ExecuteNonQueryAsync();
           }

           // TODO: create your repository with the same connection string
           // e.g. var sut = new SqlitePoiRepository(cs);
           var sut = CreateSut(cs);

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

       private static SqlitePoiRepository CreateSut(string connectionString) => new SqlitePoiRepository(connectionString);

       //private sealed record Poi(string Id, string Name, double Lat, double Lon); */
}
