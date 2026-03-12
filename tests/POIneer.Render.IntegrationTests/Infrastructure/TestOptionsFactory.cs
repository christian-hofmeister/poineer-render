using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.Flyway;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public static class TestOptionsFactory
{
    public static IOptions<FlywayOptions> CreateOptions(
        string executable = "flyway",
        string configFileRelativePath = "flyway.toml",
        bool debug = false)
    {
        return Options.Create(new FlywayOptions
        {
            Executable = executable,
            ConfigFileRelativePath = configFileRelativePath,
            Debug = debug
        });
    }
}
