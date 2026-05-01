using Microsoft.Extensions.Options;

namespace POIneer.Render.Infrastructure.Flyway;

public sealed class FlywayInvocationBuilder : IFlywayInvocationBuilder
{
    private readonly FlywayOptions _options;
    private readonly string _applicationBasePath;

    public FlywayInvocationBuilder(IOptions<FlywayOptions> options)
    {
        _options = options.Value;
        _applicationBasePath = AppContext.BaseDirectory;
    }

    public FlywayInvocation BuildForSqlite(string sqliteFilePath)
        => BuildSqliteMigrate(_options, _applicationBasePath, sqliteFilePath);

    private static FlywayInvocation BuildSqliteMigrate(
        FlywayOptions options,
        string root,
        string sqliteFilePath)
    {
        if (string.IsNullOrWhiteSpace(sqliteFilePath))
            throw new ArgumentException("SQLite file path must not be empty.", nameof(sqliteFilePath));

        var configFile = Path.GetFullPath(
            Path.Combine(root, options.ConfigFileRelativePath));

        if (!File.Exists(configFile))
        {
            throw new FileNotFoundException(
                $"Flyway config file was not found. Expected path: {configFile}",
                configFile);
        }

        var migrationsPath = Path.GetFullPath(
            Path.Combine(root, options.MigrationsRelativePath));

        if (!Directory.Exists(migrationsPath))
        {
            throw new DirectoryNotFoundException(
                $"Flyway migrations directory was not found. Expected path: {migrationsPath}");
        }

        var workingDir = Path.GetDirectoryName(configFile)
            ?? throw new InvalidOperationException("Config directory not found.");

        var args = new List<string>();

        if (options.Debug)
            args.Add("-X");

        args.Add($"-configFiles=\"{configFile}\"");
        args.Add($"-url=\"jdbc:sqlite:{Path.GetFullPath(sqliteFilePath)}\"");
        args.Add($"-locations=filesystem:{migrationsPath}");
        args.Add("migrate");

        return new FlywayInvocation(
            Executable: options.Executable,
            WorkingDirectory: workingDir,
            Arguments: args
        );
    }
}