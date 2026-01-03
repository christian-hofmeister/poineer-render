namespace POIneer.Render.Infrastructure.Flyway;

using System.Diagnostics;
using POIneer.Render.Abstractions.InfrastructureAbstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;

public sealed class FlywaySqliteDatabaseInitializer : ISqliteDatabaseInitializer
{
    private readonly IProcessRunner _runner;

    public FlywaySqliteDatabaseInitializer(IProcessRunner runner)
        => _runner = runner;

    public async Task InitializeAsync(FlywayInvocation inv, CancellationToken ct = default)
    {
        var allArgs = new List<string>();

        if (inv.Arguments is { Count: > 0 })
            allArgs.AddRange(inv.Arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = inv.Executable,
            WorkingDirectory = inv.WorkingDirectory,
            Arguments = string.Join(' ', allArgs),
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
