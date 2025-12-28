namespace POIneer.Render.Application.Contracts;

public sealed record FlywayInvocation(
    string FlywayExe,              // e.g. "flyway" or absolute path
    string WorkingDirectory,        // where flyway runs
    string ConfigFileFullPath,      // flyway config file (toml or conf)
    string SqliteDbFullPath,        // output sqlite file
    string Command = "migrate",     // default
    IReadOnlyList<string>? ExtraArgs = null // optional extra flags, e.g. -X
);
