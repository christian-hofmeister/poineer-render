namespace POIneer.Render.Infrastructure.Flyway;

public sealed record FlywayInvocation(
    string Executable,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments
);
