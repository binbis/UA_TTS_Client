#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;
using UA_TTS_Client.Core.Interfaces;

namespace UA_TTS_Client.Infrastructure.Automation;

/// <summary>
/// Global <c>Alt+Q</c> hotkey based on SharpHook (libuiohook).
/// </summary>
/// <remarks>
/// <para>Modifier state has to be tracked manually: <see cref="KeyboardEventData"/>
/// carries no information about whether Alt/Ctrl are held, so the
/// <see cref="KeyCode.VcLeftAlt"/> and <see cref="KeyCode.VcRightAlt"/> events are tracked separately.</para>
/// <para>The hotkey fires not when Q is pressed, but after both keys are released.
/// Otherwise the simulated <c>Ctrl+C</c> used to capture selected text would be sent
/// while the physical Alt is still held, and the application would receive <c>Ctrl+Alt+C</c>.</para>
/// <para>Our own simulated events (for example, the <c>Ctrl+C</c> from the clipboard service)
/// are ignored via <see cref="HookEventArgs.IsEventSimulated"/>.</para>
/// </remarks>
public sealed class SharpHookGlobalHotkeyService : IGlobalHotkeyService, IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(3);

    private readonly ILogger<SharpHookGlobalHotkeyService> _logger;
    private readonly object _sync = new();

    private TaskPoolGlobalHook? _hook;
    private Task? _runTask;
    private volatile bool _altPressed;
    private volatile bool _qPressed;
    private volatile bool _hotkeyArmed;
    private bool _disposed;

    /// <summary>
    /// Creates the global hotkey service.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public SharpHookGlobalHotkeyService(ILogger<SharpHookGlobalHotkeyService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public event EventHandler? HotkeyPressed;

    /// <inheritdoc />
    public void Start()
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_hook is not null)
            {
                return;
            }

            var hook = new TaskPoolGlobalHook(parallelismLevel: 1, UioHookProvider.Instance);

            hook.KeyPressed += OnKeyPressed;
            hook.KeyReleased += OnKeyReleased;
            hook.HookEnabled += OnHookEnabled;
            hook.HookDisabled += OnHookDisabled;

            _hook = hook;
            _runTask = RunHookAsync(hook);

            _logger.LogInformation("Глобальну гарячу клавішу Alt+Q активовано");
        }
    }

    /// <inheritdoc />
    public async Task StopAsync()
    {
        TaskPoolGlobalHook? hook;
        Task? runTask;

        lock (_sync)
        {
            hook = _hook;
            runTask = _runTask;
            _hook = null;
            _runTask = null;
            _altPressed = false;
            _qPressed = false;
            _hotkeyArmed = false;
        }

        if (hook is null)
        {
            return;
        }

        hook.KeyPressed -= OnKeyPressed;
        hook.KeyReleased -= OnKeyReleased;
        hook.HookEnabled -= OnHookEnabled;
        hook.HookDisabled -= OnHookDisabled;

        try
        {
            hook.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося коректно зупинити глобальний хук");
        }

        if (runTask is not null)
        {
            try
            {
                await runTask.WaitAsync(StopTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Глобальний хук не зупинився за {Timeout} с", StopTimeout.TotalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Завершення глобального хука супроводжувалося помилкою");
            }
        }

        try
        {
            hook.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка під час звільнення глобального хука");
        }

        _logger.LogInformation("Глобальну гарячу клавішу зупинено");
    }

    /// <summary>
    /// Runs the hook and observes its completion so that an exception is not lost.
    /// </summary>
    private async Task RunHookAsync(TaskPoolGlobalHook hook)
    {
        try
        {
            await hook.RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Глобальний хук клавіатури завершився з помилкою. "
                                 + "Гаряча клавіша Alt+Q недоступна; кнопка «Захопити виділене» працює");
        }
    }

    private void OnHookEnabled(object? sender, HookEventArgs e)
    {
        _logger.LogDebug("Глобальний хук увімкнено");
    }

    private void OnHookDisabled(object? sender, HookEventArgs e)
    {
        _logger.LogDebug("Глобальний хук вимкнено");
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        var keyCode = e.Data.KeyCode;

        if (keyCode is KeyCode.VcLeftAlt or KeyCode.VcRightAlt)
        {
            _altPressed = true;
            return;
        }

        if (keyCode == KeyCode.VcQ)
        {
            _qPressed = true;
            if (_altPressed)
            {
                // Auto-repeat of Q does not cause repeated triggers: arm only once.
                _hotkeyArmed = true;
            }
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        var keyCode = e.Data.KeyCode;

        if (keyCode is KeyCode.VcLeftAlt or KeyCode.VcRightAlt)
        {
            _altPressed = false;
            TryRaiseHotkey();
        }
        else if (keyCode == KeyCode.VcQ)
        {
            _qPressed = false;
            TryRaiseHotkey();
        }
    }

    /// <summary>
    /// Raises the hotkey once both of its keys have been released.
    /// </summary>
    /// <remarks>
    /// This is essential: while the physical Alt is held, a simulated <c>Ctrl+C</c>
    /// reaches the application as <c>Ctrl+Alt+C</c> and copies nothing.
    /// </remarks>
    private void TryRaiseHotkey()
    {
        if (!_hotkeyArmed || _altPressed || _qPressed)
        {
            return;
        }

        _hotkeyArmed = false;
        RaiseHotkeyPressed();
    }

    /// <summary>
    /// Notifies subscribers outside the hook thread so that keyboard processing is not blocked.
    /// </summary>
    private void RaiseHotkeyPressed()
    {
        var handler = HotkeyPressed;
        if (handler is null)
        {
            return;
        }

        _logger.LogDebug("Гаряча клавіша Alt+Q натиснута");

        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка в обробнику події HotkeyPressed");
            }
        });
    }

    /// <summary>
    /// Stops the hook and releases resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            StopAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Помилка під час зупинки глобального хука в Dispose");
        }
    }
}
