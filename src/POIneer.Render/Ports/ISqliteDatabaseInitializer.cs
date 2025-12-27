namespace POIneer.Render.Ports;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(string outputSqlitePath, CancellationToken ct = default);
}
