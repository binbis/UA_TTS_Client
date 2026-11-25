#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Interfaces;
// The namespace itself is named "Process", so the BCL type needs an alias.
using DiagnosticProcess = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace UA_TTS_Client.Infrastructure.Process;

/// <summary>
/// Starts and stops the packaged TTS server executable and waits for its health endpoint.
/// </summary>
/// <remarks>
/// The server runs without a console window (<c>CreateNoWindow</c>); its stdout/stderr are drained
/// and discarded because the server already mirrors every line into its own log file
/// (<see cref="IServerProcessService.LogFilePath"/>). Only a process started by this client is
/// stopped; a server started by hand (or already listening on the configured address) is adopted
/// and left untouched.
/// </remarks>
public sealed class ServerProcessService : IServerProcessService, IDisposable
{
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ReadyPollInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ProcessExitTimeout = TimeSpan.FromSeconds(10);

    private static readonly string ExecutableName =
        OperatingSystem.IsWindows() ? "UA_TTS_Server.exe" : "UA_TTS_Server";

    private readonly ITtsApiClient _apiClient;
    private readonly ILogger<ServerProcessService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private DiagnosticProcess? _process;
    private bool _disposed;

    /// <summary>
    /// Creates the service.
    /// </summary>
    /// <param name="apiClient">TTS server client (used to wait for the health endpoint).</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public ServerProcessService(ITtsApiClient apiClient, ILogger<ServerProcessService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public ServerProcessState State { get; private set; } = ServerProcessState.NotRunning;

    /// <inheritdoc />
    public bool IsStartedByClient { get; private set; }

    /// <inheritdoc />
    public string LogFilePath => _logFilePath;

    private string _logFilePath =
        Path.Combine(AppContext.BaseDirectory, "UA_TTS_Server", "logs", "server.log");

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public string GetDefaultExecutablePath() =>
        Path.Combine(AppContext.BaseDirectory, "UA_TTS_Server", ExecutableName);

    /// <inheritdoc />
    public async Task StartAsync(string? executablePath, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_process is { HasExited: false })
            {
                SetState(ServerProcessState.Running);
                return;
            }

            // A server may already answer (started by hand, or left running by a previous
            // client session). Adopt it instead of starting a second instance on the same port.
            if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogInformation("Сервер уже запущено — використовую його");
                IsStartedByClient = false;
                SetState(ServerProcessState.Running);
                return;
            }

            var resolvedPath = ResolveExecutablePath(executablePath);
            if (!File.Exists(resolvedPath))
            {
                SetState(ServerProcessState.Failed);
                throw new FileNotFoundException(
                    $"Не знайдено виконуваний файл сервера: {resolvedPath}. " +
                    "Розпакуйте пакет сервера поруч із клієнтом (тека UA_TTS_Server) або вкажіть шлях у налаштуваннях.");
            }

            // The server writes its log next to its own executable.
            _logFilePath = Path.Combine(
                Path.GetDirectoryName(resolvedPath) ?? AppContext.BaseDirectory, "logs", "server.log");

            _logger.LogInformation("Запуск сервера: {Path}", resolvedPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = resolvedPath,
                WorkingDirectory = Path.GetDirectoryName(resolvedPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            _process = DiagnosticProcess.Start(startInfo)
                ?? throw new InvalidOperationException($"Не вдалося запустити сервер: {resolvedPath}");
            // Drain the pipes so the child never blocks on a full buffer. The lines are
            // discarded: the server mirrors them into its own log file.
            _process.OutputDataReceived += (_, _) => { };
            _process.ErrorDataReceived += (_, _) => { };
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            IsStartedByClient = true;
            SetState(ServerProcessState.Starting);

            var deadline = DateTime.UtcNow + ReadyTimeout;
            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_process.HasExited)
                {
                    SetState(ServerProcessState.Failed);
                    throw new InvalidOperationException(
                        $"Сервер завершився до запуску (код {_process.ExitCode}). Деталі — у лозі: {LogFilePath}");
                }

                if (await IsHealthyAsync(cancellationToken).ConfigureAwait(false))
                {
                    SetState(ServerProcessState.Running);
                    _logger.LogInformation("Сервер запущено");
                    return;
                }

                await Task.Delay(ReadyPollInterval, cancellationToken).ConfigureAwait(false);
            }

            KillProcessTree();
            SetState(ServerProcessState.Failed);
            throw new TimeoutException(
                $"Сервер не відповів за {ReadyTimeout.TotalMinutes:0} хв. Деталі — у лозі: {LogFilePath}");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Зупинка сервера");
            cancellationToken.ThrowIfCancellationRequested();

            var killed = false;
            if (_process is { HasExited: false })
            {
                KillProcessTree();
                killed = true;
            }
            else
            {
                _process?.Dispose();
                _process = null;
            }

            // Leftover instances of the packaged server (started by hand, adopted
            // by this client, or left by another session) share its executable
            // name. This button is an explicit manual request, so stop them too.
            killed |= KillLeftoverServerProcesses();

            IsStartedByClient = false;
            SetState(ServerProcessState.NotRunning);
            if (killed)
            {
                _logger.LogInformation("Сервер зупинено");
            }
            else
            {
                _logger.LogInformation("Зупинення сервера: процес не знайдено");
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ReadLogTailAsync(int lineCount, CancellationToken cancellationToken = default)
    {
        if (lineCount <= 0 || !File.Exists(LogFilePath))
        {
            return [];
        }

        var lines = await File.ReadAllLinesAsync(LogFilePath, cancellationToken).ConfigureAwait(false);
        return lines.Length <= lineCount ? lines : lines[^lineCount..];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // The server is deliberately left running on client exit (manual management):
        // the next client session adopts it via the health check. Only handles are released.
        _process?.Dispose();
        _gate.Dispose();
    }

    /// <summary>Checks the health endpoint once; any transport failure means "not ready yet".</summary>
    private async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _apiClient.GetHealthAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TtsApiException)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health-перевірка сервера під час запуску");
            return false;
        }
    }

    /// <summary>Resolves the executable: explicit path, default location, then the dev build folder.</summary>
    private static string ResolveExecutablePath(string? executablePath)
    {
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return executablePath.Trim();
        }

        var defaultPath = Path.Combine(AppContext.BaseDirectory, "UA_TTS_Server", ExecutableName);
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        // Development convenience: dotnet run from the repository root, package built by PyInstaller.
        var devPath = Path.Combine(
            Directory.GetCurrentDirectory(), "Server_py", "dist", "UA_TTS_Server", ExecutableName);
        return File.Exists(devPath) ? devPath : defaultPath;
    }

    private void KillProcessTree()
    {
        var process = _process;
        _process = null;
        if (process is null)
        {
            return;
        }

        KillAndWait(process);
        process.Dispose();
    }

    /// <summary>
    /// Kills every still-running process with the packaged server's executable name.
    /// </summary>
    /// <returns>Whether at least one process was killed.</returns>
    private bool KillLeftoverServerProcesses()
    {
        var killed = false;
        var processName = Path.GetFileNameWithoutExtension(ExecutableName);
        foreach (var leftover in DiagnosticProcess.GetProcessesByName(processName))
        {
            using (leftover)
            {
                if (leftover.HasExited)
                {
                    continue;
                }

                _logger.LogInformation("Зупинка процесу сервера (PID {Id})", leftover.Id);
                KillAndWait(leftover);
                killed = true;
            }
        }

        return killed;
    }

    /// <summary>
    /// Kills a process (whole tree) and waits for it. On Windows falls back to
    /// <c>taskkill /T /F</c>, which reliably reaches the child processes that
    /// <see cref="DiagnosticProcess.Kill(bool)"/> sometimes misses.
    /// </summary>
    private void KillAndWait(DiagnosticProcess process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                if (!process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds))
                {
                    TryTaskKill(process.Id);
                    process.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Процес сервера вже завершився під час зупинки");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося зупинити процес сервера (PID {Id})", process.Id);
        }
    }

    private void TryTaskKill(int processId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "taskkill",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("/PID");
        startInfo.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("/T");
        startInfo.ArgumentList.Add("/F");
        using var taskkill = DiagnosticProcess.Start(startInfo);
        taskkill?.WaitForExit((int)ProcessExitTimeout.TotalMilliseconds);
    }

    private void SetState(ServerProcessState state)
    {
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

}
