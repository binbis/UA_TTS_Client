#nullable enable

using System;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UA_TTS_Client.Infrastructure.Logging;

/// <summary>
/// Writes application log lines to a file next to the client executable.
/// </summary>
/// <remarks>
/// Default path: <c>&lt;client folder&gt;/logs/client.log</c>, mirroring the server log
/// (which lives in <c>UA_TTS_Server/logs/server.log</c> next to the client).
/// The folder must be writable. The file is opened in append mode with a shared lock;
/// log lines are flushed immediately so a crash cannot lose the tail. A file larger
/// than 10 MB is rotated to <c>client.log.1</c> once at provider creation.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxLogFileSize = 10L * 1024 * 1024;

    private readonly object _gate = new();
    private readonly string _logPath;

    /// <summary>
    /// Creates the provider with the default log file path.
    /// </summary>
    public FileLoggerProvider()
        : this(GetDefaultLogFilePath())
    {
    }

    /// <summary>
    /// Creates the provider with an explicitly specified log file path (used in tests).
    /// </summary>
    /// <param name="logPath">Full path of the log file.</param>
    /// <exception cref="ArgumentException"><paramref name="logPath"/> is empty.</exception>
    public FileLoggerProvider(string logPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logPath);
        _logPath = logPath;

        var directory = Path.GetDirectoryName(_logPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (File.Exists(_logPath) && new FileInfo(_logPath).Length > MaxLogFileSize)
        {
            File.Move(_logPath, _logPath + ".1", overwrite: true);
        }
    }

    /// <summary>Full path of the log file.</summary>
    public string LogPath => _logPath;

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>Returns the default log file path: <c>&lt;client folder&gt;/logs/client.log</c>.</summary>
    public static string GetDefaultLogFilePath() =>
        Path.Combine(AppContext.BaseDirectory, "logs", "client.log");

    private void Write(LogLevel logLevel, string categoryName, string message)
    {
        var line = string.Create(
            null,
            $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{logLevel}] {categoryName}: {message}{Environment.NewLine}");
        lock (_gate)
        {
            File.AppendAllText(_logPath, line, Encoding.UTF8);
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (exception is not null)
            {
                message = string.Create(null, $"{message}{Environment.NewLine}{exception}");
            }

            provider.Write(logLevel, categoryName, message);
        }
    }
}
