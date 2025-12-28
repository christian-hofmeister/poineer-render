namespace POIneer.Render.Infrastructure.Flyway;

using POIneer.Render.Application.Contracts;

public static class FlywayInvocationBuilder
{
    public static FlywayInvocation BuildForSqliteMigrate(
        FlywayOptions options,
        string repoRoot,
        string outputSqliteFullPath)
    {
        var configFileFullPath = Path.GetFullPath(Path.Combine(
            repoRoot,
            options.ConfigFileRelativePath));

        var configDir = Path.GetDirectoryName(configFileFullPath)
                       ?? throw new InvalidOperationException("Config directory not found.");

        // Build args in a single place.
        var args = new List<string>();

        // NOTE: Flyway CLI uses -configFiles (plural) for multiple files.
        args.Add($"-configFiles=\"{configFileFullPath}\"");
        args.Add($"-url=jdbc:sqlite:{outputSqliteFullPath}");

        return new FlywayInvocation(
            FlywayExe: options.Executable,
            WorkingDirectory: configDir,
            ConfigFileFullPath: configFileFullPath,
            SqliteDbFullPath: outputSqliteFullPath,
            ExtraArgs: args
        );
    }
}
