namespace POIneer.Render.Application.Contracts;

public sealed record FlywayInvocation(
    string Executable,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments
);
