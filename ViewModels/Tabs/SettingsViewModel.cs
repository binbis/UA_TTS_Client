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
/// Вкладка налаштувань: адреса сервера, голос, швидкість, гучність і гаряча клавіша.
/// </summary>
/// <remarks>
/// Ця ViewModel є джерелом істини для <see cref="QuickTtsViewModel"/>: синтез бере
/// поточний голос і швидкість саме звідси. Гучність синхронізується з міні-плеєром
/// в обидва боки.
/// </remarks>
public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private const double MinSpeed = 0.5;
    private const double MaxSpeed = 2.0;

    private readonly ISettingsService _settingsService;
    private readonly ITtsApiClient _apiClient;
    private readonly PlaybackControlViewModel _playback;
    private readonly ILogger<SettingsViewModel> _logger;

    private bool _syncingVolume;
    private bool _disposed;

    /// <summary>
    /// Створює ViewModel налаштувань.
    /// </summary>
    /// <param name="settingsService">Сервіс збереження конфігурації.</param>
    /// <param name="apiClient">Клієнт TTS-сервера (для списку голосів).</param>
    /// <param name="playback">Міні-плеєр, з яким синхронізується гучність.</param>
    /// <param name="initialConfig">Конфігурація, завантажена на старті застосунку.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
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

    /// <summary>Адреса TTS-сервера.</summary>
    [ObservableProperty]
    public partial string ServerUrl { get; set; } = AppConfig.DefaultServerUrl;

    /// <summary>Список голосів, отриманий від сервера.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> Voices { get; set; } = [];

    /// <summary>Вибраний голос.</summary>
    [ObservableProperty]
    public partial string? SelectedVoice { get; set; }

    /// <summary>Швидкість мовлення (від 0.5 до 2.0).</summary>
    [ObservableProperty]
    public partial double Speed { get; set; } = 1.0;

    /// <summary>Гучність відтворення (від 0.0 до 1.0).</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>Гаряча клавіша захоплення виділеного тексту.</summary>
    [ObservableProperty]
    public partial string Hotkey { get; set; } = AppConfig.DefaultHotkey;

    /// <summary>Чи запускати озвучення автоматично після <c>Alt+Q</c>.</summary>
    [ObservableProperty]
    public partial bool AutoSynthesizeOnHotkey { get; set; } = true;

    /// <summary>Чи активувати вікно й перемикатися на Quick TTS після <c>Alt+Q</c>.</summary>
    [ObservableProperty]
    public partial bool ActivateOnHotkey { get; set; }

    /// <summary>Режим поділу тексту на фрагменти.</summary>
    [ObservableProperty]
    public partial TextChunkingMode SelectedChunkingMode { get; set; } = TextChunkingMode.MaxLength;

    /// <summary>Кількість фрагментів у пакеті (від 1 до 10).</summary>
    [ObservableProperty]
    public partial int BatchSize { get; set; } = SpeechQueueOptions.DefaultBatchSize;

    /// <summary>Формат збереження озвученого тексту.</summary>
    [ObservableProperty]
    public partial AudioExportFormat SelectedExportFormat { get; set; } = AudioExportFormat.Mp4;

    /// <summary>Повідомлення про успішну дію.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Повідомлення про помилку.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Ознака виконання тривалої операції.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>
    /// Завантажує список голосів із сервера.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshVoicesAsync(cancellationToken);
    }

    /// <summary>
    /// Оновлює список голосів із сервера.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
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
    /// Зберігає налаштування на диск.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
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
            ChunkingMode = SelectedChunkingMode,
            BatchSize = batchSize,
            ExportFormat = SelectedExportFormat
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
    /// Застосовує завантажену конфігурацію до властивостей.
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
        SelectedChunkingMode = config.ChunkingMode;
        BatchSize = Math.Clamp(config.BatchSize, SpeechQueueOptions.MinBatchSize, SpeechQueueOptions.MaxBatchSize);
        SelectedExportFormat = config.ExportFormat;
    }

    /// <summary>Округлює значення до кроку 0.1.</summary>
    private static double RoundToStep(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>Передає зміну гучності міні-плеєру.</summary>
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

    /// <summary>Підхоплює зміну гучності, зроблену в міні-плеєрі.</summary>
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

    /// <summary>Відписується від подій міні-плеєра.</summary>
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
