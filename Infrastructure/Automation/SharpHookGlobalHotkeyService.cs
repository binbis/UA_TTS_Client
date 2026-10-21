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
/// Глобальна гаряча клавіша <c>Alt+Q</c> на базі SharpHook (libuiohook).
/// </summary>
/// <remarks>
/// <para>Стан модифікаторів доводиться тримати самостійно: у <see cref="KeyboardEventData"/>
/// немає інформації про затиснуті Alt/Ctrl, тому окремо відслідковуються події
/// <see cref="KeyCode.VcLeftAlt"/> та <see cref="KeyCode.VcRightAlt"/>.</para>
/// <para>Гаряча клавіша спрацьовує не в момент натискання Q, а після відпускання обох
/// клавіш. Інакше симуляція <c>Ctrl+C</c> для захоплення виділеного тексту надсилалася б,
/// поки фізичний Alt ще затиснутий, і застосунок отримував би <c>Ctrl+Alt+C</c>.</para>
/// <para>Власні симульовані події (наприклад, <c>Ctrl+C</c> від сервісу буфера обміну)
/// ігноруються через <see cref="HookEventArgs.IsEventSimulated"/>.</para>
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
    /// Створює сервіс глобальної гарячої клавіші.
    /// </summary>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> дорівнює <see langword="null"/>.</exception>
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
    /// Запускає хук і спостерігає за його завершенням, щоб не втратити виняток.
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
                // Автоповтор Q не створює повторних спрацювань: озброюємось один раз.
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
    /// Повідомляє про гарячу клавішу, коли обидві її клавіші вже відпущені.
    /// </summary>
    /// <remarks>
    /// Це принципово: поки фізичний Alt затиснутий, симульований <c>Ctrl+C</c> приходить
    /// у застосунок як <c>Ctrl+Alt+C</c> і нічого не копіює.
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
    /// Повідомляє підписників поза потоком хука, щоб не блокувати обробку клавіатури.
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
    /// Зупиняє хук і звільняє ресурси.
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
