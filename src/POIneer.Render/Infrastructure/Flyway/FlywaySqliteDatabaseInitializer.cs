using System.Diagnostics;
using Microsoft.Extensions.Logging;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Ports;

namespace POIneer.Render.Infrastructure.Flyway;

public sealed class FlywaySqliteDatabaseInitializer : ISqliteDatabaseInitializer
{
    private readonly ILogger<FlywaySqliteDatabaseInitializer> _logger;
    private readonly IProcessRunner _runner;
    private readonly IFlywayInvocationBuilder _invocationBuilder;

    public FlywaySqliteDatabaseInitializer(
        IProcessRunner runner,
        IFlywayInvocationBuilder invocationBuilder,
        ILogger<FlywaySqliteDatabaseInitializer> logger)
    {
        _runner = runner;
        _invocationBuilder = invocationBuilder;
        _logger = logger;
    }

    public async Task InitializeAsync(
        string sqliteFilePath,
        CancellationToken ct = default)
    {
        _logger.LogDebug("Running Flyway for database: {DbPath}", sqliteFilePath);

        var inv = _invocationBuilder.BuildForSqlite(sqliteFilePath);
        _logger.LogDebug("Flyway args: {Args}", string.Join(" ", inv.Arguments));

        var startInfo = new ProcessStartInfo
        {
            FileName = inv.Executable,
            WorkingDirectory = inv.WorkingDirectory,
            Arguments = string.Join(' ', inv.Arguments ?? Array.Empty<string>()),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var result = await _runner.RunAsync(startInfo, ct);

        _logger.LogDebug("Flyway StandardOutput: {StdOut}", result.StandardOutput);
        _logger.LogDebug("Flyway StandardError: {StdErr}", result.StandardError);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Flyway failed (ExitCode={result.ExitCode}). StdErr: {result.StandardError}");
        }

    }
}
