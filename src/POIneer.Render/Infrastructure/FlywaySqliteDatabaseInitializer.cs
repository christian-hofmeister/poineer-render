using System.Diagnostics;
using Microsoft.Extensions.Logging;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Ports;

namespace POIneer.Render.Infrastructure;

public sealed class FlywaySqliteDatabaseInitializer : ISqliteDatabaseInitializer
{
    private readonly ILogger<FlywaySqliteDatabaseInitializer> _logger;

    public FlywaySqliteDatabaseInitializer(
        ILogger<FlywaySqliteDatabaseInitializer> log)
    {
        _logger = log;
    }

    public async Task InitializeAsync(
        string outputSqlitePath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Running Flyway migrations...");

        var repoRoot = RepoRootLocator.Find();

        var flywayConfigPathFull = Path.Combine(
            repoRoot,
            "migrations",
            "flyway-poi.toml"
        );

        var outputSqlitePathFull = Path.GetFullPath(outputSqlitePath);

        _logger.LogInformation("Using Flyway config: {FlywayConfig}", flywayConfigPathFull);
        _logger.LogInformation("Initializing SQLite database at: {OutputSqlite}", outputSqlitePath);

        var configDir = Path.GetDirectoryName(flywayConfigPathFull)!;

        _logger.LogInformation("Flyway config directory: {ConfigDir}", configDir);
        _logger.LogInformation("flyway url: -url=jdbc:sqlite:{outputSqlitePathFull} ", outputSqlitePathFull);

        if (!File.Exists(flywayConfigPathFull))
            throw new FileNotFoundException("Flyway config file not found", flywayConfigPathFull);

        var psi = new ProcessStartInfo
        {
            FileName = "flyway",
            WorkingDirectory = configDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-X");
        psi.ArgumentList.Add($"-configFiles={flywayConfigPathFull}");
        psi.ArgumentList.Add($"-url=jdbc:sqlite:{outputSqlitePathFull}");
        psi.ArgumentList.Add("migrate");

        _logger.LogInformation("psi.arguments: '{psi.Arguments}'", psi.Arguments);

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start flyway process.");

        var stdout = await p.StandardOutput.ReadToEndAsync(ct);
        var stderr = await p.StandardError.ReadToEndAsync(ct);

        await p.WaitForExitAsync(ct);

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Flyway failed with exit code {p.ExitCode}.{Environment.NewLine}{stdout}{Environment.NewLine}{stderr}");
        }
    }
}
