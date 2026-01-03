namespace POIneer.Render.Infrastructure.Flyway;

using POIneer.Render.Application.Contracts;

public static class FlywayInvocationBuilder
{
    public static FlywayInvocation BuildSqliteMigrate(
        FlywayOptions options,
        string repoRoot,
        string sqliteDbFullPath)
    {
        var configFile = Path.GetFullPath(
            Path.Combine(repoRoot, options.ConfigFileRelativePath));

        var workingDir = Path.GetDirectoryName(configFile)
            ?? throw new InvalidOperationException("Config directory not found.");

        var args = new List<string>();

        if (options.Debug)
            args.Add("-X");

        args.Add($"-configFiles=\"{configFile}\"");
        args.Add($"-url=jdbc:sqlite:{Path.GetFullPath(sqliteDbFullPath)}");
        args.Add("migrate");

        return new FlywayInvocation(
            Executable: options.Executable,
            WorkingDirectory: workingDir,
            Arguments: args
        );
    }
}
