namespace POIneer.Render.Infrastructure.Pathing;

public static class RepoRootLocator
{
    public static string Find()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "migrations")) ||
                File.Exists(Path.Combine(dir.FullName, "poineer-render.sln")) ||
                Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Repo root not found.");
    }
}
