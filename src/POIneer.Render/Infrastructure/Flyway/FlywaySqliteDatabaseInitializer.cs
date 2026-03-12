using System.Diagnostics;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Ports;

namespace POIneer.Render.Infrastructure.Flyway;

public sealed class FlywaySqliteDatabaseInitializer : ISqliteDatabaseInitializer
{
    private readonly IProcessRunner _runner;
    private readonly IFlywayInvocationBuilder _invocationBuilder;

    public FlywaySqliteDatabaseInitializer(
        IProcessRunner runner,
        IFlywayInvocationBuilder invocationBuilder)
    {
        _runner = runner;
        _invocationBuilder = invocationBuilder;
    }

    public async Task InitializeAsync(
        string sqliteFilePath,
        CancellationToken ct = default)
    {
        var inv = _invocationBuilder.BuildForSqlite(sqliteFilePath);

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

        if (result.ExitCode != 0)
            throw new InvalidOperationException(
                $"Flyway failed (ExitCode={result.ExitCode}). StdErr: {result.StandardError}");
    }
}
