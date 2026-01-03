namespace POIneer.Render.Ports;

using POIneer.Render.Application.Contracts;

public interface ISqliteDatabaseInitializer
{
    Task InitializeAsync(FlywayInvocation invocation, CancellationToken ct = default);
}