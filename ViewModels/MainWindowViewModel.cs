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
/// Main window ViewModel: coordinates the tabs, server status, and the global hotkey.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(15);

    /// <summary>Index of the Quick TTS tab in the main window.</summary>
    private const int QuickTtsTabIndex = 0;

    private readonly ITtsApiClient _apiClient;
    private readonly IGlobalHotkeyService _hotkeyService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly CancellationTokenSource _cts = new();

    private bool _disposed;
    private bool _hotkeyCaptureInProgress;

    /// <summary>
    /// Creates the main window ViewModel.
    /// </summary>
    /// <param name="quickTts">Quick TTS tab.</param>
    /// <param name="settings">Settings tab.</param>
    /// <param name="playback">Mini player.</param>
    /// <param name="apiClient">TTS server client (for health checks).</param>
    /// <param name="hotkeyService">Global hotkey service.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
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

    /// <summary>Quick TTS tab.</summary>
    public QuickTtsViewModel QuickTts { get; }

    /// <summary>Settings tab.</summary>
    public SettingsViewModel Settings { get; }

    /// <summary>Mini player.</summary>
    public PlaybackControlViewModel Playback { get; }

    /// <summary>Reader tab (placeholder).</summary>
    public ReaderViewModel Reader { get; } = new();

    /// <summary>Character Voice tab (placeholder).</summary>
    public CharacterVoiceViewModel CharacterVoice { get; } = new();

    /// <summary>
    /// Raised after <c>Alt+Q</c> when the main window needs to be shown and activated.
    /// </summary>
    /// <remarks>
    /// The subscriber in <c>App.axaml.cs</c> calls <c>Window.Activate()</c>. On Wayland,
    /// the compositor may ignore the request, so activation is done on a best-effort basis.
    /// </remarks>
    public event EventHandler? ActivateRequested;

    /// <summary>Current TTS server status.</summary>
    [ObservableProperty]
    public partial ServerStatus ServerStatus { get; set; } = ServerStatus.Unknown;

    /// <summary>Short server status text for the top bar.</summary>
    [ObservableProperty]
    public partial string ServerStatusText { get; set; } = "Перевірка…";

    /// <summary>Additional server details (device, number of voices, error text).</summary>
    [ObservableProperty]
    public partial string ServerDetails { get; set; } = string.Empty;

    /// <summary>Index of the selected tab in the main window (0 — Quick TTS).</summary>
    [ObservableProperty]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>Flag indicating that the server is online (used by the status indicator).</summary>
    public bool IsServerOnline => ServerStatus == ServerStatus.Online;

    /// <summary>
    /// Initializes the tabs, the hotkey, and the periodic server health check.
    /// </summary>
    /// <remarks>Called once after the main window is opened.</remarks>
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
            // The application is closing during initialization.
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
    /// Checks the server status manually (the "Перевірити" (Check) button).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task RefreshServerStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            var health = await _apiClient.GetHealthAsync(cancellationToken);

            var details = health.Device is null
                ? $"{health.Voices} голосів"
                : $"{health.Device} · {health.Voices} голосів";

            if (!string.IsNullOrWhiteSpace(health.Dictionary?.Error))
            {
                details += " · словник: помилка";
            }

            ServerDetails = details;
            ServerStatusText = $"Онлайн · {details}";
            ServerStatus = ServerStatus.Online;
        }
        catch (OperationCanceledException)
        {
            // Cancellation during application shutdown.
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
    /// Periodically checks server availability while the application is running.
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
            // Normal monitor shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Моніторинг стану сервера зупинився");
        }
    }

    /// <summary>
    /// Starts handling the Alt+Q hotkey on the UI thread.
    /// </summary>
    private void OnHotkeyPressed(object? sender, EventArgs e)
    {
        OnUiThread(() => _ = HandleHotkeyAsync());
    }

    /// <summary>
    /// Runs the Alt+Q sequence: capture → activation → auto-synthesis.
    /// </summary>
    /// <remarks>
    /// <para>Capture always comes first: the simulated <c>Ctrl+C</c> must go to the active window of another app.
    /// If the window is activated earlier, empty text will be copied.</para>
    /// <para>A repeated <c>Alt+Q</c> during capture is ignored.</para>
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
            // The application is closing.
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

    /// <summary>Updates the derived online-status flag.</summary>
    partial void OnServerStatusChanged(ServerStatus value) => OnPropertyChanged(nameof(IsServerOnline));

    /// <summary>
    /// Stops monitoring and unsubscribes from the hotkey.
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
