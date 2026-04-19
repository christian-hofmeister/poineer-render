namespace POIneer.Render.Infrastructure.Process;

public static class ProcessUtils
{
    public static bool IsExecutableAvailable(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        if (Path.IsPathRooted(executable))
        {
            return File.Exists(executable);
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        if (OperatingSystem.IsWindows())
        {
            var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var directory in paths)
            {
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(directory, executable + ext);
                    if (File.Exists(candidate))
                    {
                        return true;
                    }
                }
            }
        }

        foreach (var directory in paths)
        {
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return true;
            }
        }

        return false;
    }
    public static string? ResolveExecutablePath(string executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return null;
        }

        if (Path.IsPathRooted(executable))
        {
            return File.Exists(executable) ? executable : null;
        }

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }
        var paths = path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        if (OperatingSystem.IsWindows())
        {
            var extensions = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
                .Split(';', StringSplitOptions.RemoveEmptyEntries);

            foreach (var directory in paths)
            {
                foreach (var ext in extensions)
                {
                    var candidate = Path.Combine(directory, executable + ext);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
            }
            return null;
        }

        foreach (var directory in paths)
        {
            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static bool IsExecutableAvailable(string executable, out string? path)
    {
        path = ResolveExecutablePath(executable);
        return path != null;
    }
}