namespace POIneer.Render.Infrastructure.Pathing;

public static class ContentRootResolver
{
    private const string AppSettingsFileName = "appsettings.json";
    private static readonly string ProjectDirectoryRelativeToBuildOutput = Path.Combine("..", "..", "..");

    public static string Resolve()
        => Resolve(Directory.GetCurrentDirectory(), AppContext.BaseDirectory);

    public static string Resolve(string currentDirectory, string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);

        var projectDirectory = Path.GetFullPath(Path.Combine(
            baseDirectory,
            ProjectDirectoryRelativeToBuildOutput));

        var candidates = new[] {
            currentDirectory,
            projectDirectory,
            baseDirectory };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, AppSettingsFileName)))
                return candidate;
        }

        throw new FileNotFoundException(
            $"Could not find 'appsettings.json' in any of the candidate directories: "
            + string.Join(", ", candidates.Select(c => $"'{c}'")));
    }
}
