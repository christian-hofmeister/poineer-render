namespace POIneer.Render.Infrastructure.Flyway;

public sealed class FlywayOptions
{
    public const string SectionName = "Flyway";

    public string Executable { get; init; } = "flyway";
    public string ConfigFileRelativePath { get; init; } = "migrations/flyway-poi.toml";
    public bool Debug { get; init; } = false;
}