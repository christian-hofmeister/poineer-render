public static class TemporaryDirectoryNameHelper
{
    public static string CreateSafeFolderName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "Folder name must not be empty.",
                nameof(value));
        }

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(c, '_');
        }
        // Replace common path separators just in case, even though they should be covered by GetInvalidFileNameChars. (win vs unix)
        value = value
           .Replace('/', '_')
           .Replace('\\', '_');

        value = value
            .Replace(Path.DirectorySeparatorChar, '_')
            .Replace(Path.AltDirectorySeparatorChar, '_');

        const int maxLength = 40;

        if (value.Length > maxLength)
        {
            value = value[..maxLength];
        }

        return value;
    }
}