using FluentAssertions;
using Microsoft.Data.Sqlite;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite;

public sealed class SqliteDatasetValidatorTests
{
    private readonly SqliteDatasetValidator _sut = new();

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileDoesNotExist()
    {
        await using var tempDir = TestTemporaryDirectories.Create("validate-missing-file", false);
        var path = Path.Combine(tempDir.DirectoryPath, "does-not-exist.sqlite");

        var result = await _sut.ValidateAsync(path, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("does not exist"));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileIsEmpty()
    {
        await using var tempDir = TestTemporaryDirectories.Create("validate-empty-file", false);
        var path = Path.Combine(tempDir.DirectoryPath, "empty.sqlite");
        await File.WriteAllBytesAsync(path, []);

        var result = await _sut.ValidateAsync(path, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("is empty"));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenFileIsNotAValidSqliteDatabase()
    {
        await using var tempDir = TestTemporaryDirectories.Create("validate-corrupt-file", false);
        var path = Path.Combine(tempDir.DirectoryPath, "corrupt.sqlite");
        await File.WriteAllTextAsync(path, "this is definitely not a sqlite database");

        var result = await _sut.ValidateAsync(path, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("could not be opened or queried"));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsInvalid_WhenRequiredTableIsMissing()
    {
        await using var tempDir = TestTemporaryDirectories.Create("validate-missing-table", false);
        var path = Path.Combine(tempDir.DirectoryPath, "no-poi-table.sqlite");

        await using (var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(path)))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE some_other_table (id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var result = await _sut.ValidateAsync(path, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("'poi'") && e.Contains("missing"));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsValid_WhenDatabaseHasRequiredTableAndPassesIntegrityCheck()
    {
        await using var tempDir = TestTemporaryDirectories.Create("validate-valid-database", false);

        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);

        var result = await _sut.ValidateAsync(dbPath, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}
