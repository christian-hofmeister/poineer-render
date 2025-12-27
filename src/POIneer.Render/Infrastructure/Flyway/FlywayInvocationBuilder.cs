namespace POIneer.Render.Infrastructure.Flyway;

using POIneer.Render.Infrastructure.Pathing;

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

        // Important: Flyway CLI expects -configFiles (plural) and for TOML that's fine.
        // Also: keep args ordering stable.
        var args = string.Join(' ',
            options.Debug ? "-X" : "",
            $"-configFiles=\"{configFileFullPath}\"",
            $"-url=jdbc:sqlite:{outputSqliteFullPath}",
            "migrate"
        ).Trim();

        return new FlywayInvocation(
            Executable: options.Executable,
            WorkingDirectory: configDir,
            ConfigFileFullPath: configFileFullPath,
            OutputSqliteFullPath: outputSqliteFullPath,
            Arguments: args
        );
    }
}
