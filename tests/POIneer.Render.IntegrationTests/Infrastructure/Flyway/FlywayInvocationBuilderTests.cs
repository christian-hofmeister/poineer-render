using Xunit;
using POIneer.Render.Infrastructure.Flyway;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace POIneer.Render.IntegrationTests.Infrastructure.Flyway;

public sealed class FlywayInvocationBuilderTests
{
    [Fact]
    public void BuildForSqlite_BuildsInvocation()
    {
        // Arrange
        var root = AppContext.BaseDirectory;

        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            ConfigFileRelativePath = Path.Combine("test-flyway", "flyway.toml"),
            MigrationsRelativePath = Path.Combine("test-flyway", "migrations"),
            Debug = true
        });

        var configDir = Path.Combine(root, "test-flyway");
        Directory.CreateDirectory(configDir);

        var configFile = Path.Combine(configDir, "flyway.toml");
        File.WriteAllText(configFile, "# test flyway config");

        var migrationsDir = Path.Combine(configDir, "migrations");
        Directory.CreateDirectory(migrationsDir);


        var sut = new FlywayInvocationBuilder(options);

        // Act
        var inv = sut.BuildForSqlite(Path.Combine(root, "out", "db.sqlite"));

        // Assert
        Assert.Equal("flyway", inv.Executable);
        Assert.NotNull(inv.Arguments);
        Assert.Contains("-X", inv.Arguments);
        Assert.Contains("migrate", inv.Arguments);

        // configFiles contains absolute path
        var expectedConfig = Path.GetFullPath(Path.Combine(root, "test-flyway", "flyway.toml"));
        Assert.Contains($"-configFiles=\"{expectedConfig}\"", inv.Arguments);

        // working dir is config dir
        Assert.Equal(Path.GetDirectoryName(expectedConfig), inv.WorkingDirectory);

        // url points at absolute sqlite path
        var expectedDb = Path.GetFullPath(Path.Combine(root, "out", "db.sqlite"));
        Assert.Contains(inv.Arguments, a => a.StartsWith("-url=", StringComparison.Ordinal));
        Assert.Contains(inv.Arguments, a => a.Contains($"jdbc:sqlite:{expectedDb}", StringComparison.Ordinal));


    }

    [Fact]
    public void BuildForSqlite_BuildsInvocation_FromOptionsAndContentRoot()
    {
        var root = AppContext.BaseDirectory;

        var configDir = Path.Combine(root, "test-flyway");
        Directory.CreateDirectory(configDir);

        var configFile = Path.Combine(configDir, "flyway.toml");
        File.WriteAllText(configFile, "# test flyway config");

        var migrationsPath = Path.Combine(root, "test-migrations", "sql", "poi");
        Directory.CreateDirectory(migrationsPath);

        var options = TestOptionsFactory.CreateOptions(
            debug: true,
            configFileRelativePath: Path.Combine("test-flyway", "flyway.toml"),
            migrationsRelativePath: Path.Combine("test-migrations", "sql", "poi"));

        var sut = new FlywayInvocationBuilder(options);

        var dbPath = Path.Combine(root, "out", "db.sqlite");
        var inv = sut.BuildForSqlite(dbPath);

        Assert.Equal("flyway", inv.Executable);

        var expectedConfig = Path.GetFullPath(Path.Combine(root, "test-flyway", "flyway.toml"));
        Assert.Equal(Path.GetDirectoryName(expectedConfig), inv.WorkingDirectory);

        Assert.Contains("-X", inv.Arguments);
        Assert.Contains($"-configFiles=\"{expectedConfig}\"", inv.Arguments);

        var expectedDb = Path.GetFullPath(dbPath);
        Assert.Contains(inv.Arguments, a =>
            a.Contains($"jdbc:sqlite:{expectedDb}", StringComparison.Ordinal));

        Assert.Contains("migrate", inv.Arguments);
    }
}
