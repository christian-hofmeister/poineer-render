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
        // inv.ExtraArgs should already contain everything (including command if you put it there),
        // OR you keep command separate - but don't duplicate flags here.
        var args = new List<string>();

        if (inv.ExtraArgs is { Count: > 0 })
            args.AddRange(inv.ExtraArgs);

        // If you model command separately, append it exactly once:
        args.Add(inv.Command);

        var startInfo = new ProcessStartInfo
        {
            FileName = inv.FlywayExe,
            WorkingDirectory = inv.WorkingDirectory,
            Arguments = string.Join(' ', args),
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
