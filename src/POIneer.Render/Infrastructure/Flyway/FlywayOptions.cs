public sealed record FlywayOptions(
    string Executable = "flyway",
    string ConfigFileRelativePath = "migrations/flyway-poi.toml",
    bool Debug = false
);
