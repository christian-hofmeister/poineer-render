namespace POIneer.Render.Ports;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(FlywayInvocation invocation, CancellationToken ct = default);
}
