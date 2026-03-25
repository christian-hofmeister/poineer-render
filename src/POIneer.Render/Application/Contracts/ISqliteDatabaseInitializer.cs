namespace POIneer.Render.Ports;

using POIneer.Render.Infrastructure.Flyway;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(string sqliteFilePath, CancellationToken ct);
}