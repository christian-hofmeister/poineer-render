using System.Diagnostics;

namespace POIneer.Render.Infrastructure.Process;

public static class ProcessUtils
{
    public static bool IsExecutableAvailable(string executable, string versionArgs = "-v")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = versionArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return false;

            if (!process.WaitForExit(3000))
            {
                try { process.Kill(true); } catch { }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}