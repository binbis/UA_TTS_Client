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
/// Захоплює виділений текст на Wayland: симулює <c>Ctrl+C</c> через uinput і читає буфер обміну.
/// </summary>
/// <remarks>
/// <para>Wayland не дозволяє читати виділення чужого вікна напряму, тому єдиний робочий
/// шлях — інжекція <c>Ctrl+C</c> у поточне активне вікно.</para>
/// <para>Основна реалізація симуляції — SharpHook <see cref="EventSimulator"/>.
/// Якщо він недоступний, використовується CLI <c>ydotool</c> (демон уже працює в системі).</para>
/// <para>Перед натисканням <c>Ctrl+C</c> сервіс відпускає Alt: інакше активне вікно
/// отримало б <c>Ctrl+Alt+C</c>, бо гаряча клавіша <c>Alt+Q</c> ще фізично затиснута.</para>
/// </remarks>
public sealed class WaylandClipboardAutomationService : IClipboardAutomationService, IDisposable
{
    private const string ApplicationName = "UA_TTS_Client";

    /// <summary>Затримка між симуляцією Ctrl+C і читанням буфера (мс).</summary>
    private const int ClipboardDelayMilliseconds = 120;

    private static readonly TimeSpan YdotoolTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<WaylandClipboardAutomationService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _simulatorSync = new();

    private EventSimulator? _simulator;
    private bool _simulatorUnavailable;
    private bool _disposed;

    /// <summary>
    /// Створює сервіс захоплення виділеного тексту.
    /// </summary>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> дорівнює <see langword="null"/>.</exception>
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

            var simulated = await SimulateCopyAsync(cancellationToken).ConfigureAwait(false);
            if (!simulated)
            {
                _logger.LogWarning("Не вдалося симулювати Ctrl+C — текст не захоплено");
                return string.Empty;
            }

            // Буфер обміну оновлюється асинхронно: без паузи читається старе значення.
            await Task.Delay(ClipboardDelayMilliseconds, cancellationToken).ConfigureAwait(false);

            var text = await ReadClipboardAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Захоплено {Length} символів із буфера обміну", text.Length);
            return text;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Читає текст із буфера обміну.
    /// </summary>
    /// <remarks>
    /// TextCopy на Linux працює лише через X11 (<c>xsel</c>), тому в чистому Wayland-сеансі
    /// без XWayland він падає. У такому разі використовується <c>wl-paste</c>.
    /// </remarks>
    private async Task<string> ReadClipboardAsync(CancellationToken cancellationToken)
    {
        try
        {
            var text = await ClipboardService.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "TextCopy не зміг прочитати буфер обміну — пробую wl-paste");
        }

        if (!OperatingSystem.IsLinux())
        {
            return string.Empty;
        }

        var wayland = await TryRunClipboardCommandAsync("wl-paste", ["--no-newline"], cancellationToken)
            .ConfigureAwait(false);
        if (wayland is not null)
        {
            return wayland;
        }

        var x11 = await TryRunClipboardCommandAsync("xsel", ["--clipboard", "--output"], cancellationToken)
            .ConfigureAwait(false);
        return x11 ?? string.Empty;
    }

    /// <summary>
    /// Запускає консольну утиліту буфера обміну й повертає її stdout.
    /// </summary>
    /// <returns>Текст або <see langword="null"/>, якщо утиліта недоступна чи завершилася з помилкою.</returns>
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
            timeoutCts.CancelAfter(YdotoolTimeout);

            var outputTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            var output = await outputTask.ConfigureAwait(false);

            return process.ExitCode == 0 ? output : null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{FileName} не встиг завершитися за {Timeout} с", fileName, YdotoolTimeout.TotalSeconds);
            return null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            _logger.LogDebug(ex, "Утиліта {FileName} недоступна", fileName);
            return null;
        }
    }

    /// <summary>
    /// Симулює <c>Alt up → Ctrl+C</c> через SharpHook, а за потреби — через <c>ydotool</c>.
    /// </summary>
    /// <returns><see langword="true"/>, якщо натискання вдалося надіслати.</returns>
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
    /// Повертає симулятор SharpHook, створюючи його ліниво (ініціалізація uinput недешева).
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
    /// Резервний шлях: викликає <c>ydotool key</c> з кодами клавіш Linux input.
    /// </summary>
    /// <remarks>
    /// Коди: <c>56</c> — KEY_LEFTALT, <c>100</c> — KEY_RIGHTALT, <c>29</c> — KEY_LEFTCTRL,
    /// <c>46</c> — KEY_C. Суфікс <c>:1</c> — натискання, <c>:0</c> — відпускання.
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
    /// Звільняє симулятор і семафор.
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
