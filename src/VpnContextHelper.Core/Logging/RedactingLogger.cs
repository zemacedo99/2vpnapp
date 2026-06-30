using VpnContextHelper.Core.Diagnostics;

namespace VpnContextHelper.Core.Logging;

public enum LogLevel
{
    Debug,
    Info,
    Warn,
    Error,
}

public interface ILog
{
    void Log(LogLevel level, string message);
}

/// <summary>
/// Minimal structured logger that ALWAYS routes messages through the <see cref="Redactor"/> before
/// they are written, so a stray secret can never land in a log file. Thread-safe append.
/// </summary>
public sealed class RedactingLogger : ILog, IDisposable
{
    private readonly Redactor _redactor;
    private readonly TextWriter? _file;
    private readonly bool _toConsole;
    private readonly object _gate = new();

    public RedactingLogger(Redactor redactor, string? filePath = null, bool toConsole = true)
    {
        _redactor = redactor;
        _toConsole = toConsole;
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            _file = new StreamWriter(new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true,
            };
        }
    }

    public void Log(LogLevel level, string message)
    {
        var safe = _redactor.Redact(message);
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss} [{level,-5}] {safe}";
        lock (_gate)
        {
            if (_toConsole) Console.WriteLine(line);
            _file?.WriteLine(line);
        }
    }

    public void Info(string m) => Log(LogLevel.Info, m);
    public void Warn(string m) => Log(LogLevel.Warn, m);
    public void Error(string m) => Log(LogLevel.Error, m);

    public void Dispose()
    {
        lock (_gate) _file?.Dispose();
    }
}
