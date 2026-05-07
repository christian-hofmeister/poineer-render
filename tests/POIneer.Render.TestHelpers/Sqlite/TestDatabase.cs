using Microsoft.Data.Sqlite;

namespace POIneer.Render.TestHelpers.Sqlite;

public static class SqliteTestDatabase
{
    public static string CreateConnectionString(string dbPath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Pooling = false
        }.ToString();
    }
}