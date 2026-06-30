namespace VpnContextHelper.Core.SystemAccess;

public sealed record CommandResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Runs a read-only external command and captures its output. Abstracted so that
/// <see cref="VpnContextHelper.Core.SystemAccess.LiveSystemInspector"/> can be tested with a fake.
/// Implementations MUST only ever be used for read-only diagnostic commands.
/// </summary>
public interface ICommandRunner
{
    CommandResult Run(string fileName, string arguments, int timeoutMs = 15000);
}
