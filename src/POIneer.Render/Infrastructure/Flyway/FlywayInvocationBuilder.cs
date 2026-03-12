using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace POIneer.Render.Infrastructure.Flyway;

public sealed class FlywayInvocationBuilder : IFlywayInvocationBuilder
{
    private readonly FlywayOptions _options;
    private readonly string _root;

    public FlywayInvocationBuilder(
        IOptions<FlywayOptions> options,
        IHostEnvironment env)
    {
        _options = options.Value;
        _root = env.ContentRootPath;
    }

    public FlywayInvocation BuildForSqlite(string sqliteFilePath)
        => BuildSqliteMigrate(_options, _root, sqliteFilePath);
    public FlywayInvocation BuildSqliteMigrate(FlywayOptions options, string root, string sqliteFilePath)
    {
        if (string.IsNullOrWhiteSpace(sqliteFilePath))
            throw new ArgumentException("SQLite file path must not be empty.", nameof(sqliteFilePath));

        var configFile = Path.GetFullPath(
            Path.Combine(root, options.ConfigFileRelativePath));

        var workingDir = Path.GetDirectoryName(configFile)
            ?? throw new InvalidOperationException("Config directory not found.");

        var args = new List<string>();

        if (options.Debug)
            args.Add("-X");

        args.Add($"-configFiles=\"{configFile}\"");
        args.Add($"-url=\"jdbc:sqlite:{Path.GetFullPath(sqliteFilePath)}\"");
        args.Add("migrate");

        return new FlywayInvocation(
            Executable: options.Executable,
            WorkingDirectory: workingDir,
            Arguments: args
        );
    }
}
