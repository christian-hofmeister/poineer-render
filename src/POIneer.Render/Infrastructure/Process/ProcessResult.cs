namespace POIneer.Render.Infrastructure.Process
{
    public sealed record ProcessResult(
        int ExitCode,
        string StandardOutput,
        string StandardError
    );
}