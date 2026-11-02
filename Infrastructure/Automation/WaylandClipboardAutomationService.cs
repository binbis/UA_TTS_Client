#nullable enable

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SharpHook.Data;
using SharpHook.Providers;
using SharpHook.Simulation;
using Microsoft.Extensions.Logging;
using TextCopy;
using UA_TTS_Client.Core.Interfaces;

namespace UA_TTS_Client.Infrastructure.Automation;

/// <summary>
/// Captures selected text on Wayland: simulates <c>Ctrl+C</c> via uinput and reads the clipboard.
/// </summary>
/// <remarks>
/// <para>Wayland does not allow reading another window's selection directly, so the only working
/// path is injecting <c>Ctrl+C</c> into the current active window.</para>
/// <para>The primary simulation implementation is SharpHook <see cref="EventSimulator"/>.
/// If it is unavailable, the <c>ydotool</c> CLI is used (its daemon is already running on the system).</para>
/// <para>Before pressing <c>Ctrl+C</c>, the service releases Alt: otherwise the active window would
/// receive <c>Ctrl+Alt+C</c>, because the <c>Alt+Q</c> hotkey is still physically held down.</para>
/// </remarks>
public sealed class WaylandClipboardAutomationService : IClipboardAutomationService, IDisposable
{
    private const string ApplicationName = "UA_TTS_Client";

    /// <summary>Clipboard polling interval after simulating Ctrl+C (ms).</summary>
    private const int ClipboardPollIntervalMilliseconds = 50;

    /// <summary>How long to wait for the clipboard contents to change after Ctrl+C.</summary>
    private static readonly TimeSpan ClipboardChangeTimeout = TimeSpan.FromMilliseconds(900);

    private static readonly TimeSpan YdotoolTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ClipboardCommandTimeout = TimeSpan.FromSeconds(2);

    private readonly ILogger<WaylandClipboardAutomationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _simulatorSync = new();

    private EventSimulator? _simulator;
    private bool _simulatorUnavailable;
    private bool _disposed;

    /// <summary>
    /// Creates the selected text capture service.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public WaylandClipboardAutomationService(ILogger<WaylandClipboardAutomationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<string> CaptureSelectedTextAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Remember the current contents: if Ctrl+C does not work, the clipboard stays unchanged
            // and we do not hand the user old text as "captured".
            var previous = await ReadClipboardAsync(cancellationToken).ConfigureAwait(false);

            var simulated = await SimulateCopyAsync(cancellationToken).ConfigureAwait(false);
            if (!simulated)
            {
                _logger.LogWarning("Не вдалося симулювати Ctrl+C — текст не захоплено");
                return string.Empty;
            }

            var deadline = DateTime.UtcNow + ClipboardChangeTimeout;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(ClipboardPollIntervalMilliseconds, cancellationToken).ConfigureAwait(false);

                var current = await ReadClipboardAsync(cancellationToken).ConfigureAwait(false);
                if (!string.Equals(current, previous, StringComparison.Ordinal))
                {
                    _logger.LogDebug("Захоплено {Length} символів із буфера обміну", current.Length);
                    return current;
                }
            }

            _logger.LogWarning("Буфер обміну не змінився після Ctrl+C: нічого не виділено "
                               + "або активне вікно не прийняло Ctrl+C");
            return string.Empty;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reads text from the clipboard.
    /// </summary>
    /// <remarks>
    /// On Linux the clipboard is read only via <c>wl-paste</c> (Wayland). X11 is not supported,
    /// so the obsolete X11 fallback has been removed. On Windows TextCopy (Win32 Clipboard) is used,
    /// because <c>wl-paste</c> is not available there.
    /// </remarks>
    private async Task<string> ReadClipboardAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsLinux())
        {
            if (!IsWaylandSession())
            {
                _logger.LogWarning("X11-сеанс не підтримується: буфер обміну доступний лише у Wayland через wl-paste");
                return string.Empty;
            }

            var wayland = await TryRunClipboardCommandAsync("wl-paste", ["--no-newline"], cancellationToken)
                .ConfigureAwait(false);
            if (wayland is null)
            {
                _logger.LogWarning("wl-paste недоступний або завершився з помилкою. "
                                   + "Установіть wl-clipboard (пакет wl-clipboard)");
                return string.Empty;
            }

            return wayland;
        }

        return await ReadWithTextCopyAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads the clipboard via TextCopy (used only on Windows).
    /// </summary>
    private async Task<string> ReadWithTextCopyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ClipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TextCopy не зміг прочитати буфер обміну");
            return string.Empty;
        }
    }

    /// <summary>Whether the application is running in a Wayland session.</summary>
    private static bool IsWaylandSession() =>
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));

    /// <summary>
    /// Runs a console clipboard utility and returns its stdout.
    /// </summary>
    /// <returns>The text, or <see langword="null"/> if the utility is unavailable or exited with an error.</returns>
    private async Task<string?> TryRunClipboardCommandAsync(
        string fileName,
        string[] arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var startInfo = new ProcessStartInfo(fileName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(ClipboardCommandTimeout);

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);

            return process.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{FileName} не встиг завершитися за {Timeout} с", fileName, ClipboardCommandTimeout.TotalSeconds);
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogDebug(ex, "Утиліта {FileName} недоступна", fileName);
            return null;
        }
    }

    /// <summary>
    /// Simulates <c>Alt up → Ctrl+C</c> via SharpHook, or via <c>ydotool</c> if needed.
    /// </summary>
    /// <returns><see langword="true"/> if the key presses were sent successfully.</returns>
    private async Task<bool> SimulateCopyAsync(CancellationToken cancellationToken)
    {
        var simulator = GetOrCreateSimulator();

        if (simulator is not null)
        {
            var result = await Task.Run(() =>
            {
                try
                {
                    return simulator.Sequence()
                        .AddKeyRelease(KeyCode.VcLeftAlt)
                        .AddKeyRelease(KeyCode.VcRightAlt)
                        .AddKeyPress(KeyCode.VcLeftControl)
                        .AddKeyPress(KeyCode.VcC)
                        .AddKeyRelease(KeyCode.VcC)
                        .AddKeyRelease(KeyCode.VcLeftControl)
                        .Simulate();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Помилка симуляції клавіатури через SharpHook");
                    return UioHookResult.Failure;
                }
            }, cancellationToken).ConfigureAwait(false);

            if (result == UioHookResult.Success)
            {
                return true;
            }

            _logger.LogWarning("SharpHook не зміг симулювати Ctrl+C (результат {Result}) — пробую ydotool", result);
        }

        return await TrySimulateWithYdotoolAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the SharpHook simulator, creating it lazily (uinput initialization is expensive).
    /// </summary>
    private EventSimulator? GetOrCreateSimulator()
    {
        lock (_simulatorSync)
        {
            if (_simulatorUnavailable)
            {
                return null;
            }

            if (_simulator is not null)
            {
                return _simulator;
            }

            try
            {
                _simulator = EventSimulator.Create(ApplicationName, UioHookProvider.Instance);
                _logger.LogInformation("Симуляцію клавіатури через SharpHook ініціалізовано");
                return _simulator;
            }
            catch (Exception ex)
            {
                _simulatorUnavailable = true;
                _logger.LogWarning(ex, "Не вдалося ініціалізувати симуляцію SharpHook — використовуватиму ydotool");
                return null;
            }
        }
    }

    /// <summary>
    /// Fallback path: invokes <c>ydotool key</c> with Linux input key codes.
    /// </summary>
    /// <remarks>
    /// Codes: <c>56</c> — KEY_LEFTALT, <c>100</c> — KEY_RIGHTALT, <c>29</c> — KEY_LEFTCTRL,
    /// <c>46</c> — KEY_C. The <c>:1</c> suffix means press, <c>:0</c> means release.
    /// </remarks>
    private async Task<bool> TrySimulateWithYdotoolAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsLinux())
        {
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo("ydotool")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            foreach (var argument in new[] { "key", "56:0", "100:0", "29:1", "46:1", "46:0", "29:0" })
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                _logger.LogWarning("Не вдалося запустити ydotool");
                return false;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(YdotoolTimeout);

            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                _logger.LogDebug("Ctrl+C симульовано через ydotool");
                return true;
            }

            _logger.LogWarning("ydotool завершився з кодом {Code}: {Error}", process.ExitCode, stderr.Trim());
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ydotool не встиг завершитися за {Timeout} с", YdotoolTimeout.TotalSeconds);
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogWarning(ex, "Не вдалося викликати ydotool");
            return false;
        }
    }

    /// <summary>
    /// Releases the simulator and the semaphore.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        lock (_simulatorSync)
        {
            _simulator?.Dispose();
            _simulator = null;
        }

        _gate.Dispose();
    }
}
