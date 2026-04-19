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
        var root = Path.GetTempPath();

        IHostEnvironment env = new FakeHostEnvironment
        {
            ContentRootPath = root,
            EnvironmentName = "Test",
            ApplicationName = "POIneer.Render.Tests"
        };
        //var executable = "flyway"; TODO - make this test work on CI by using the actual flyway executable path, or mock the file system to fake it
        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            ConfigFileRelativePath = "flyway.toml",
            Debug = true
        });



        var sut = new FlywayInvocationBuilder(options, env);

        // Act
        var inv = sut.BuildForSqlite(Path.Combine(root, "out", "db.sqlite"));

        // Assert
        Assert.Equal("flyway", inv.Executable);
        Assert.NotNull(inv.Arguments);
        Assert.Contains("-X", inv.Arguments);
        Assert.Contains("migrate", inv.Arguments);

        // configFiles contains absolute path
        var expectedConfig = Path.GetFullPath(Path.Combine(root, "flyway.toml"));
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
        var root = Path.GetTempPath();

        IHostEnvironment env = new FakeHostEnvironment
        {
            ContentRootPath = root,
            EnvironmentName = "Test",
            ApplicationName = "POIneer.Render.Tests"
        };


        var options = TestOptionsFactory.CreateOptions(debug: true);

        var sut = new FlywayInvocationBuilder(options, env);

        var dbPath = Path.Combine(root, "out", "db.sqlite");
        var inv = sut.BuildForSqlite(dbPath);

        Assert.Equal("flyway", inv.Executable);

        var expectedConfig = Path.GetFullPath(Path.Combine(root, "flyway.toml"));
        Assert.Equal(Path.GetDirectoryName(expectedConfig), inv.WorkingDirectory);

        Assert.Contains("-X", inv.Arguments);
        Assert.Contains($"-configFiles=\"{expectedConfig}\"", inv.Arguments);

        var expectedDb = Path.GetFullPath(dbPath);
        Assert.Contains(inv.Arguments, a =>
            a.Contains($"jdbc:sqlite:{expectedDb}", StringComparison.Ordinal));

        Assert.Contains("migrate", inv.Arguments);
    }
}
