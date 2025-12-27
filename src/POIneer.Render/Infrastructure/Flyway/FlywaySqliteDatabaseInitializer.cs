using System.Diagnostics;
using Microsoft.Extensions.Logging;
using POIneer.Render.Abstractions.InfrastructureAbstractions;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Ports;

namespace POIneer.Render.Infrastructure;

public sealed class FlywaySqliteDatabaseInitializer : ISqliteDatabaseInitializer
{
    private readonly ILogger<FlywaySqliteDatabaseInitializer> _logger;
    private readonly IProcessRunner _processRunner;

    public FlywaySqliteDatabaseInitializer(
        IProcessRunner processRunner,
        ILogger<FlywaySqliteDatabaseInitializer> log)
    {
        _logger = log;
        _processRunner = processRunner;
    }

    public async Task InitializeAsync(FlywayInvocation invocation, CancellationToken ct = default)
    {
        _logger.LogInformation("Running Flyway migrations...");


        _logger.LogInformation("Flyway Working directory: {WorkingDirectory}", invocation.WorkingDirectory);
        _logger.LogInformation("flyway url: -url=jdbc:sqlite:{outputSqlitePathFull} ", invocation.OutputSqliteFullPath);

        if (!File.Exists(invocation.ConfigFileFullPath))
            throw new FileNotFoundException("Flyway config file not found", invocation.ConfigFileFullPath);

        var psi = new ProcessStartInfo
        {
            FileName = invocation.Executable,
            WorkingDirectory = invocation.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add("-X");
        psi.ArgumentList.Add($"-configFiles={invocation.ConfigFileFullPath}");
        psi.ArgumentList.Add($"-url=jdbc:sqlite:{invocation.OutputSqliteFullPath}");
        psi.ArgumentList.Add("migrate");

        _logger.LogInformation("psi.arguments: '{psi.Arguments}'", psi.Arguments);

        var result = await _processRunner.RunAsync(psi, ct);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Flyway failed: {ExitCode}\nSTDOUT:\n{StdOut}\nSTDERR:\n{StdErr}",
                result.ExitCode, result.StdOut, result.StdErr);
            throw new InvalidOperationException($"Flyway failed with exit code {result.ExitCode}.");
        }
    }
}
