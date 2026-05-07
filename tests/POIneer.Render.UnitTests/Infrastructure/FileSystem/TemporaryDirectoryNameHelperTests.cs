using Xunit;

namespace POIneer.Render.Tests.TestHelpers;

public sealed class TemporaryDirectoryNameHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void CreateSafeFolderName_Throws_WhenValueIsNullOrWhiteSpace(
        string? value)
    {
        var act = () => TemporaryDirectoryNameHelper.CreateSafeFolderName(value!);

        Assert.Throws<ArgumentException>(act);
    }

    [Fact]
    public void CreateSafeFolderName_ReplacesDirectorySeparators()
    {
        var result = TemporaryDirectoryNameHelper.CreateSafeFolderName(
            "foo/bar\\baz");

        Assert.Equal("foo_bar_baz", result);
    }

    [Fact]
    public void CreateSafeFolderName_ReplacesInvalidFileNameCharacters()
    {
        var invalidChar = Path.GetInvalidFileNameChars()[0];

        var input = $"foo{invalidChar}bar";

        var result = TemporaryDirectoryNameHelper.CreateSafeFolderName(input);

        Assert.Equal("foo_bar", result);
    }

    [Fact]
    public void CreateSafeFolderName_ReplacesAllInvalidFileNameCharacters()
    {
        var invalidChars = Path.GetInvalidFileNameChars();

        var input = new string(invalidChars);

        var result = TemporaryDirectoryNameHelper.CreateSafeFolderName(input);

        Assert.DoesNotContain(result, c => invalidChars.Contains(c));
        Assert.All(result, c => Assert.Equal('_', c));
    }

    [Fact]
    public void CreateSafeFolderName_TruncatesLongNames()
    {
        var input = new string('a', 100);

        var result = TemporaryDirectoryNameHelper.CreateSafeFolderName(input);

        Assert.Equal(40, result.Length);
        Assert.Equal(new string('a', 40), result);
    }

    [Fact]
    public void CreateSafeFolderName_KeepsValidName()
    {
        var result = TemporaryDirectoryNameHelper.CreateSafeFolderName(
            "flyway-applies-migrations");

        Assert.Equal("flyway-applies-migrations", result);
    }
}