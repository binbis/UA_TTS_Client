#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.ViewModels.Components;
using UA_TTS_Client.ViewModels.Tabs;
using ServerStatus = UA_TTS_Client.Core.Enums.ServerStatus;

namespace UA_TTS_Client.ViewModels;

/// <summary>
/// Головна ViewModel вікна: координує вкладки, стан сервера та глобальну гарячу клавішу.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>Індекс вкладки Quick TTS у головному вікні.</summary>
    private const int QuickTtsTabIndex = 0;

    private readonly ITtsApiClient _apiClient;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly CancellationTokenSource _cts = new();

    private bool _disposed;
    private bool _hotkeyCaptureInProgress;

    /// <summary>
    /// Створює головну ViewModel.
    /// </summary>
    /// <param name="quickTts">Вкладка Quick TTS.</param>
    /// <param name="settings">Вкладка налаштувань.</param>
    /// <param name="playback">Міні-плеєр.</param>
    /// <param name="apiClient">Клієнт TTS-сервера (для health-check).</param>
    /// <param name="hotkeyService">Глобальна гаряча клавіша.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public MainWindowViewModel(
        QuickTtsViewModel quickTts,
        SettingsViewModel settings,
        PlaybackControlViewModel playback,
        ITtsApiClient apiClient,
        IGlobalHotkeyService hotkeyService,
        ILogger<MainWindowViewModel> logger)
    {
        QuickTts = quickTts ?? throw new ArgumentNullException(nameof(quickTts));
        Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        Playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Вкладка Quick TTS.</summary>
    public QuickTtsViewModel QuickTts { get; }

    /// <summary>Вкладка налаштувань.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Міні-плеєр.</summary>
    public PlaybackControlViewModel Playback { get; }

    /// <summary>Вкладка Reader (заглушка).</summary>
    public ReaderViewModel Reader { get; } = new();

    /// <summary>Вкладка Character Voice (заглушка).</summary>
    public CharacterVoiceViewModel CharacterVoice { get; } = new();

    /// <summary>
    /// Виникає після <c>Alt+Q</c>, коли треба показати й активувати головне вікно.
    /// </summary>
    /// <remarks>
    /// Підписник у <c>App.axaml.cs</c> викликає <c>Window.Activate()</c>. На Wayland
    /// композитор може проігнорувати запит — активація виконується за принципом best effort.
    /// </remarks>
    public event EventHandler? ActivateRequested;

    /// <summary>Поточний стан TTS-сервера.</summary>
    [ObservableProperty]
    public partial ServerStatus ServerStatus { get; set; } = ServerStatus.Unknown;

    /// <summary>Стислий текст стану сервера для верхньої панелі.</summary>
    [ObservableProperty]
    public partial string ServerStatusText { get; set; } = "Перевірка…";

    /// <summary>Додаткові відомості про сервер (пристрій, кількість голосів, текст помилки).</summary>
    [ObservableProperty]
    public partial string ServerDetails { get; set; } = string.Empty;

    /// <summary>Індекс вибраної вкладки головного вікна (0 — Quick TTS).</summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>Ознака того, що сервер онлайн (використовується для індикатора).</summary>
    public bool IsServerOnline => ServerStatus == ServerStatus.Online;

    /// <summary>
    /// Ініціалізує вкладки, гарячу клавішу та періодичну перевірку сервера.
    /// </summary>
    /// <remarks>Викликається один раз після відкриття головного вікна.</remarks>
    public async Task InitializeAsync()
    {
        try
        {
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.Start();

            await Settings.InitializeAsync(_cts.Token);
            await RefreshServerStatusAsync(_cts.Token);

            _ = MonitorServerAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Застосунок закривається під час ініціалізації.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка ініціалізації головного вікна");
            ServerStatus = ServerStatus.Error;
            ServerStatusText = "Помилка ініціалізації";
            ServerDetails = ex.Message;
        }
    }

    /// <summary>
    /// Перевіряє стан сервера вручну (кнопка «Перевірити»).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    [RelayCommand]
    private async Task RefreshServerStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await _apiClient.GetHealthAsync(cancellationToken);

            var details = health.Device is null
                ? $"{health.Voices} голосів"
                : $"{health.Device} · {health.Voices} голосів";

            ServerDetails = details;
            ServerStatusText = $"Онлайн · {details}";
            ServerStatus = ServerStatus.Online;
        }
        catch (OperationCanceledException)
        {
            // Скасування під час закриття застосунку.
        }
        catch (TtsApiException ex) when (ex.IsConnectionError)
        {
            ServerDetails = string.Empty;
            ServerStatusText = "Офлайн";
            ServerStatus = ServerStatus.Offline;
        }
        catch (TtsApiException ex)
        {
            _logger.LogWarning(ex, "Сервер повернув помилку під час health-check");
            ServerDetails = ex.Message;
            ServerStatusText = "Помилка сервера";
            ServerStatus = ServerStatus.Error;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка health-check");
            ServerDetails = ex.Message;
            ServerStatusText = "Помилка";
            ServerStatus = ServerStatus.Error;
        }
    }

    /// <summary>
    /// Періодично перевіряє доступність сервера, доки застосунок працює.
    /// </summary>
    private async Task MonitorServerAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(HealthCheckInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await RefreshServerStatusAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Нормальне завершення роботи монітора.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Моніторинг стану сервера зупинився");
        }
    }

    /// <summary>
    /// Запускає обробку гарячої клавіші Alt+Q на UI-потоці.
    /// </summary>
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        OnUiThread(() => _ = HandleHotkeyAsync());
    }

    /// <summary>
    /// Виконує послідовність Alt+Q: захоплення → активація → автосинтез.
    /// </summary>
    /// <remarks>
    /// <para>Захоплення завжди перше: симуляція <c>Ctrl+C</c> має піти в активне чуже вікно.
    /// Якщо активувати вікно раніше, буде скопійовано порожньо.</para>
    /// <para>Повторне <c>Alt+Q</c> під час захоплення ігнорується.</para>
    /// </remarks>
    private async Task HandleHotkeyAsync()
    {
        if (_hotkeyCaptureInProgress)
        {
            _logger.LogDebug("Alt+Q: попереднє захоплення ще виконується — пропускаю");
            return;
        }

        _hotkeyCaptureInProgress = true;

        try
        {
            var captured = await QuickTts.CaptureForHotkeyAsync(_cts.Token);
            if (!captured)
            {
                return;
            }

            if (Settings.ActivateOnHotkey)
            {
                SelectedTabIndex = QuickTtsTabIndex;
                ActivateRequested?.Invoke(this, EventArgs.Empty);
            }

            if (Settings.AutoSynthesizeOnHotkey)
            {
                await QuickTts.SynthesizeCurrentTextAsync(_cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            // Застосунок закривається.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка обробки гарячої клавіші Alt+Q");
        }
        finally
        {
            _hotkeyCaptureInProgress = false;
        }
    }

    /// <summary>Оновлює похідну ознаку онлайн-стану.</summary>
    partial void OnServerStatusChanged(ServerStatus value) => OnPropertyChanged(nameof(IsServerOnline));

    /// <summary>
    /// Зупиняє моніторинг і відписується від гарячої клавіші.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _cts.Cancel();
        _cts.Dispose();
    }
}
