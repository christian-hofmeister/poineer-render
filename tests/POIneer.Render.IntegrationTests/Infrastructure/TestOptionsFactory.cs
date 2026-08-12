using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Infrastructure.Flyway;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public static class TestOptionsFactory
{
    public static IOptions<FlywayOptions> CreateOptions(
        bool debug = false,
    string executable = "flyway",
    string configFileRelativePath = "flyway.toml",
    string migrationsRelativePath = "migrations/sql/poi")
    {
        return Options.Create(new FlywayOptions
        {
            Executable = executable,
            ConfigFileRelativePath = configFileRelativePath,
            MigrationsRelativePath = migrationsRelativePath,
            Debug = debug
        });
    }

    public static IOptions<PublisherOptions> CreatePublisherOptions(
        string destinationDir,
        DatasetPublishOverwritePolicy overwritePolicy = DatasetPublishOverwritePolicy.Skip,
        string schemaVersion = "1")
    {
        return Options.Create(new PublisherOptions
        {
            DestinationDir = destinationDir,
            OverwritePolicy = overwritePolicy,
            SchemaVersion = schemaVersion
        });
    }
}
