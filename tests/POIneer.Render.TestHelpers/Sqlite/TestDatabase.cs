using Microsoft.Data.Sqlite;

namespace POIneer.Render.TestHelpers.Sqlite;

public static class SqliteTestDatabase
{
    public static string CreateConnectionString(string dbPath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            // Important for tests to ensure that each connection is independent and doesn't interfere with others
            Pooling = false
        }.ToString();
    }
}