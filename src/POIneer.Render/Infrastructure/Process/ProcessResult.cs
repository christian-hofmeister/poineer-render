namespace POIneer.Render.Abstractions.InfrastructureAbstractions
{
    public sealed record ProcessResult(
        int ExitCode,
        string StdOut,
        string StdErr);
}