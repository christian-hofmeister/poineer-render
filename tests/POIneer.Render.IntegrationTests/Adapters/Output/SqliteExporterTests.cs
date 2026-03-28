using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Process;
using Xunit;

namespace POIneer.Render.IntegrationTests.Adapters.Output;

public sealed class SqliteExporterTests
{
    [Fact]
    public async Task ExportAsync_HappyPath_InsertsMultiplePois()
    {
        var root = FindRepositoryRoot();

        await using var tempDir = TemporaryDirectory.Create("sqlite-exporter-test", true);
        var dbPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");

        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            ConfigFileRelativePath = "migrations/flyway-poi.toml",
            Debug = false
        });


        IHostEnvironment env = new FakeHostEnvironment
        {
            ContentRootPath = root,
            EnvironmentName = "Test",
            ApplicationName = "POIneer.Render.IntegrationTests"
        };

        var processRunner = new ProcessRunner();
        var invocationBuilder = new FlywayInvocationBuilder(options, env);
        var databaseInitializer = new FlywaySqliteDatabaseInitializer(
            processRunner,
            invocationBuilder);

        await databaseInitializer.InitializeAsync(dbPath, CancellationToken.None);

        var sut = new SqliteExporter();

        var pois = ToAsyncEnumerable(
            new PoiDto("node/1001", "Test Cafe", "cafe", 52.5200, 13.4050),
            new PoiDto("node/1002", "Test Pharmacy", "pharmacy", 52.5205, 13.4055));

        await sut.ExportAsync(pois, dbPath, CancellationToken.None);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM poi;";
            var count = (long)(await countCommand.ExecuteScalarAsync())!;
            Assert.Equal(2, count);
        }

        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText = """
                SELECT osm_id, name, amenity, latitude, longitude
                FROM poi
                ORDER BY osm_id;
                """;

            await using var reader = await selectCommand.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("node/1001", reader.GetString(0));
            Assert.Equal("Test Cafe", reader.GetString(1));
            Assert.Equal("cafe", reader.GetString(2));
            Assert.Equal(52.5200, reader.GetDouble(3));
            Assert.Equal(13.4050, reader.GetDouble(4));

            Assert.True(await reader.ReadAsync());
            Assert.Equal("node/1002", reader.GetString(0));
            Assert.Equal("Test Pharmacy", reader.GetString(1));
            Assert.Equal("pharmacy", reader.GetString(2));
            Assert.Equal(52.5205, reader.GetDouble(3));
            Assert.Equal(13.4055, reader.GetDouble(4));

            Assert.False(await reader.ReadAsync());
        }
    }

    private static async IAsyncEnumerable<PoiDto> ToAsyncEnumerable(params PoiDto[] pois)
    {
        foreach (var poi in pois)
        {
            yield return poi;
            await Task.Yield();
        }
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (dir is not null)
        {
            var flywayConfigPath = Path.Combine(
                dir.FullName,
                "migrations",
                "flyway-poi.toml");

            if (File.Exists(flywayConfigPath))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root could not be determined.");
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}