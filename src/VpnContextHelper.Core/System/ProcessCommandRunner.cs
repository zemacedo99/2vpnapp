using System.Diagnostics;
using System.Text;

namespace VpnContextHelper.Core.SystemAccess;

/// <summary>
/// Default <see cref="ICommandRunner"/> that shells out to a process and captures stdout/stderr.
/// Used only for read-only diagnostic commands (route print, ipconfig, etc.).
/// </summary>
public sealed class ProcessCommandRunner : ICommandRunner
{
    public CommandResult Run(string fileName, string arguments, int timeoutMs = 15000)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        try
        {
            using var p = Process.Start(psi);
            if (p is null) return new CommandResult(-1, string.Empty, "failed to start process");

            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { /* best effort */ }
                return new CommandResult(-2, stdout, "timeout");
            }
            return new CommandResult(p.ExitCode, stdout, stderr);
        }
        catch (Exception ex)
        {
            return new CommandResult(-3, string.Empty, ex.Message);
        }
    }
}
