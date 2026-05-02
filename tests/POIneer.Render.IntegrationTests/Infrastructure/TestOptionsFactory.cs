using Microsoft.Extensions.Options;
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
}
