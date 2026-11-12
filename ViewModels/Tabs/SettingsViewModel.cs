#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UA_TTS_Client.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Settings tab: server address, voice, speed, volume, chunking, export format, and hotkey.
/// </summary>
/// <remarks>
/// This ViewModel is the source of truth for <see cref="QuickTtsViewModel"/>: synthesis takes the
/// current voice, speed, chunking mode and batch size from here.
/// Volume is synchronized with the mini player in both directions.
/// </remarks>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private const double MinSpeed = 0.5;
    private const double MaxSpeed = 2.0;

    private static readonly ChunkingModeOption DefaultChunkingMode =
        new(TextChunkingMode.Paragraphs, "Абзаци");

    private static readonly ExportFormatOption DefaultExportFormat =
        new(AudioExportFormat.Mp3, "MP3 (аудіо, типово)");

    private readonly ISettingsService _settingsService;
    private readonly ITtsApiClient _apiClient;
    private readonly PlaybackControlViewModel _playback;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _syncingVolume;
    private bool _disposed;

    /// <summary>
    /// Creates the settings ViewModel.
    /// </summary>
    /// <param name="settingsService">Service that saves the configuration.</param>
    /// <param name="apiClient">TTS server client (used for the voice list).</param>
    /// <param name="playback">Mini player that the volume is synchronized with.</param>
    /// <param name="initialConfig">Configuration loaded at application startup.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public SettingsViewModel(
        ISettingsService settingsService,
        ITtsApiClient apiClient,
        PlaybackControlViewModel playback,
        AppConfig initialConfig,
        ILogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArgumentNullException.ThrowIfNull(initialConfig);

        ApplyConfig(initialConfig);
        _playback.PropertyChanged += OnPlaybackPropertyChanged;
    }

    /// <summary>TTS server address.</summary>
    [ObservableProperty]
    public partial string ServerUrl { get; set; } = AppConfig.DefaultServerUrl;

    /// <summary>List of voices received from the server.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Voices { get; set; } = [];

    /// <summary>Selected voice.</summary>
    [ObservableProperty]
    public partial string? SelectedVoice { get; set; }

    /// <summary>Speech speed (from 0.5 to 2.0).</summary>
    [ObservableProperty]
    public partial double Speed { get; set; } = 1.0;

    /// <summary>Playback volume (from 0.0 to 1.0).</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>Hotkey for capturing the selected text.</summary>
    [ObservableProperty]
    public partial string Hotkey { get; set; } = AppConfig.DefaultHotkey;

    /// <summary>Whether to start speech automatically after <c>Alt+Q</c>.</summary>
    [ObservableProperty]
    public partial bool AutoSynthesizeOnHotkey { get; set; } = true;

    /// <summary>Whether to activate the window and switch to Quick TTS after <c>Alt+Q</c>.</summary>
    [ObservableProperty]
    public partial bool ActivateOnHotkey { get; set; }

    /// <summary>Chunking mode options for the list in the settings.</summary>
    public IReadOnlyList<ChunkingModeOption> ChunkingModes { get; } =
    [
        DefaultChunkingMode,
        new(TextChunkingMode.Sentences, "Речення"),
        new(TextChunkingMode.MaxLength, "До 250 символів (розумне пакування)")
    ];

    /// <summary>Audio save format options for the list in the settings.</summary>
    public IReadOnlyList<ExportFormatOption> ExportFormats { get; } =
    [
        DefaultExportFormat,
        new(AudioExportFormat.Wav, "WAV (без стиснення)")
    ];

    /// <summary>Selected mode for splitting the text into fragments.</summary>
    [ObservableProperty]
    public partial ChunkingModeOption? SelectedChunkingMode { get; set; }

    /// <summary>Number of fragments per batch (from 1 to 10).</summary>
    [ObservableProperty]
    public partial int BatchSize { get; set; } = SpeechQueueOptions.DefaultBatchSize;

    /// <summary>Whether to stop after each batch (default) or play all batches back to back.</summary>
    [ObservableProperty]
    public partial bool PauseAfterEachBatch { get; set; } = true;

    /// <summary>Selected format for saving the spoken text.</summary>
    [ObservableProperty]
    public partial ExportFormatOption? SelectedExportFormat { get; set; }

    /// <summary>Current chunking mode (with a default value).</summary>
    public TextChunkingMode ChunkingMode => SelectedChunkingMode?.Mode ?? TextChunkingMode.Paragraphs;

    /// <summary>Current save format (with a default value).</summary>
    public AudioExportFormat ExportFormat => SelectedExportFormat?.Format ?? AudioExportFormat.Mp3;

    /// <summary>Message about a successful action.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Error message.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Flag indicating that a long-running operation is in progress.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Loads the voice list from the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshVoicesAsync(cancellationToken);
    }

    /// <summary>
    /// Refreshes the voice list from the server.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task RefreshVoicesAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var voices = await _apiClient.GetVoicesAsync(cancellationToken);
            Voices = voices.ToArray();

            if (SelectedVoice is null || !Voices.Contains(SelectedVoice, StringComparer.Ordinal))
            {
                SelectedVoice = Voices.FirstOrDefault();
            }

            StatusMessage = $"Доступно голосів: {Voices.Count}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Оновлення списку голосів скасовано.";
        }
        catch (TtsApiException ex)
        {
            _logger.LogWarning(ex, "Не вдалося отримати список голосів");
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка під час отримання голосів");
            ErrorMessage = "Не вдалося отримати список голосів.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Saves the settings to disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;

        var urlText = ServerUrl.Trim();
        if (!Uri.TryCreate(urlText, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            ErrorMessage = "Адреса сервера має бути абсолютною: http:// або https://…";
            return;
        }

        var speed = RoundToStep(Math.Clamp(Speed, MinSpeed, MaxSpeed));
        var volume = RoundToStep(Math.Clamp(Volume, 0d, 1d));
        var batchSize = Math.Clamp(BatchSize, SpeechQueueOptions.MinBatchSize, SpeechQueueOptions.MaxBatchSize);

        var config = new AppConfig
        {
            ServerUrl = uri.ToString().TrimEnd('/'),
            SelectedVoice = SelectedVoice,
            Speed = speed,
            Volume = volume,
            Hotkey = string.IsNullOrWhiteSpace(Hotkey) ? AppConfig.DefaultHotkey : Hotkey,
            AutoSynthesizeOnHotkey = AutoSynthesizeOnHotkey,
            ActivateOnHotkey = ActivateOnHotkey,
            ChunkingMode = this.ChunkingMode,
            BatchSize = batchSize,
            PauseAfterEachBatch = this.PauseAfterEachBatch,
            ExportFormat = this.ExportFormat
        };

        IsBusy = true;
        try
        {
            await _settingsService.SaveAsync(config, cancellationToken);

            ServerUrl = config.ServerUrl;
            Speed = speed;
            Volume = volume;
            BatchSize = batchSize;
            StatusMessage = "Збережено. Нову адресу сервера буде застосовано після перезапуску.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Збереження скасовано.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Не вдалося зберегти налаштування");
            ErrorMessage = "Не вдалося записати файл налаштувань.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Applies the loaded configuration to the properties.
    /// </summary>
    private void ApplyConfig(AppConfig config)
    {
        ServerUrl = config.ServerUrl;
        SelectedVoice = config.SelectedVoice;
        Speed = double.IsFinite(config.Speed) ? RoundToStep(Math.Clamp(config.Speed, MinSpeed, MaxSpeed)) : 1.0;
        Volume = double.IsFinite(config.Volume) ? RoundToStep(Math.Clamp(config.Volume, 0d, 1d)) : 1.0;
        Hotkey = string.IsNullOrWhiteSpace(config.Hotkey) ? AppConfig.DefaultHotkey : config.Hotkey;
        AutoSynthesizeOnHotkey = config.AutoSynthesizeOnHotkey;
        ActivateOnHotkey = config.ActivateOnHotkey;
        SelectedChunkingMode = ChunkingModes.FirstOrDefault(option => option.Mode == config.ChunkingMode)
            ?? DefaultChunkingMode;
        BatchSize = Math.Clamp(config.BatchSize, SpeechQueueOptions.MinBatchSize, SpeechQueueOptions.MaxBatchSize);
        PauseAfterEachBatch = config.PauseAfterEachBatch;
        SelectedExportFormat = ExportFormats.FirstOrDefault(option => option.Format == config.ExportFormat)
            ?? DefaultExportFormat;
    }

    /// <summary>Rounds the value to a step of 0.1.</summary>
    private static double RoundToStep(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>Updates the derived value after the chunking mode changes.</summary>
    partial void OnSelectedChunkingModeChanged(ChunkingModeOption? value) =>
        OnPropertyChanged(nameof(ChunkingMode));

    /// <summary>Updates the derived value after the save format changes.</summary>
    partial void OnSelectedExportFormatChanged(ExportFormatOption? value) =>
        OnPropertyChanged(nameof(ExportFormat));

    /// <summary>Passes the volume change to the mini player.</summary>
    partial void OnVolumeChanged(double value)
    {
        if (_syncingVolume)
        {
            return;
        }

        _syncingVolume = true;
        try
        {
            _playback.Volume = Math.Clamp(value, 0d, 1d);
        }
        finally
        {
            _syncingVolume = false;
        }
    }

    /// <summary>Picks up the volume change made in the mini player.</summary>
    private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(PlaybackControlViewModel.Volume) || _syncingVolume)
        {
            return;
        }

        _syncingVolume = true;
        try
        {
            Volume = _playback.Volume;
        }
        finally
        {
            _syncingVolume = false;
        }
    }

    /// <summary>Unsubscribes from the mini player events.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _playback.PropertyChanged -= OnPlaybackPropertyChanged;
    }
}
