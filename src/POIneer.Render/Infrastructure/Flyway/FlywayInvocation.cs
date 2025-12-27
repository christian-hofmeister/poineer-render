public sealed record FlywayInvocation(
    string Executable,
    string WorkingDirectory,
    string ConfigFileFullPath,
    string OutputSqliteFullPath,
    string Arguments
);