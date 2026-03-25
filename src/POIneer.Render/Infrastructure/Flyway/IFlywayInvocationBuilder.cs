using POIneer.Render.Infrastructure.Flyway;

public interface IFlywayInvocationBuilder
{
    FlywayInvocation BuildForSqlite(string sqliteFilePath);
}