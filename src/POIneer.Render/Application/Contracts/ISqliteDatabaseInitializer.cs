namespace POIneer.Render.Ports;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(string sqliteFilePath, CancellationToken ct);
}