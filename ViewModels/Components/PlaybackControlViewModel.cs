#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.ViewModels.Components;

/// <summary>
/// Міні-плеєр: керує відтворенням синтезованого аудіо та показує його стан.
/// </summary>
public partial class PlaybackControlViewModel : ViewModelBase, IDisposable
{
    private const int FragmentPreviewLength = 60;

    private readonly IAudioPlayerService _audioPlayer;
    private readonly IFragmentPlaybackService _fragmentPlayback;
    private readonly ILogger<PlaybackControlViewModel> _logger;
    private bool _disposed;

    private int _lastTotalFragments;
    private int _lastTotalBatches;

    /// <summary>
    /// Створює міні-плеєр.
    /// </summary>
    /// <param name="config">Стартові налаштування (звідти береться початкова гучність).</param>
    /// <param name="audioPlayer">Сервіс відтворення.</param>
    /// <param name="fragmentPlayback">Черга фрагментів.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public PlaybackControlViewModel(
        AppConfig config,
        IAudioPlayerService audioPlayer,
        IFragmentPlaybackService fragmentPlayback,
        ILogger<PlaybackControlViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        _fragmentPlayback = fragmentPlayback ?? throw new ArgumentNullException(nameof(fragmentPlayback));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _audioPlayer.StateChanged += OnPlayerStateChanged;
        _fragmentPlayback.FragmentStarted += OnFragmentStarted;
        _fragmentPlayback.BatchCompleted += OnBatchCompleted;
        _fragmentPlayback.QueueCompleted += OnQueueCompleted;
        _fragmentPlayback.Failed += OnQueueFailed;

        // Сетер одразу застосує гучність до BASS.
        Volume = Math.Clamp(config.Volume, 0d, 1d);
    }

    /// <summary>Поточний стан відтворення.</summary>
    [ObservableProperty]
    public partial PlaybackState State { get; set; } = PlaybackState.Idle;

    /// <summary>Людиночитний опис стану для статус-бару.</summary>
    [ObservableProperty]
    public partial string StateText { get; set; } = "Готово";

    /// <summary>Символ на кнопці «пауза/продовжити».</summary>
    [ObservableProperty]
    public partial string PlayPauseGlyph { get; set; } = "▶";

    /// <summary>Ім'я голосу, яким озвучено поточний трек.</summary>
    [ObservableProperty]
    public partial string? VoiceName { get; set; }

    /// <summary>Гучність відтворення в діапазоні від 0.0 до 1.0.</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>Позиція у черзі: «Фрагмент 2 з 5 · пакет 1 з 3».</summary>
    [ObservableProperty]
    public partial string? FragmentPositionText { get; set; }

    /// <summary>Початок тексту поточного фрагмента (до 60 символів).</summary>
    [ObservableProperty]
    public partial string? CurrentFragmentText { get; set; }

    /// <summary>Стан черги для вкладки Quick TTS: завершення пакета або всієї черги.</summary>
    [ObservableProperty]
    public partial string? QueueStatusText { get; set; }

    /// <summary>Виникає на UI-потоці, коли черга фрагментів зупинилася з помилкою.</summary>
    public event EventHandler<Exception>? QueueFailed;

    /// <summary>Чи завершився пакет і черга чекає на продовження.</summary>
    [ObservableProperty]
    public partial bool IsBatchPending { get; set; }

    /// <summary>Текст кнопки продовження наступного пакета.</summary>
    [ObservableProperty]
    public partial string? ContinueButtonText { get; set; }

    /// <summary>Чи існує активна черга фрагментів.</summary>
    [ObservableProperty]
    public partial bool HasQueue { get; set; }

    /// <summary>Ознака того, що аудіо зараз відтворюється.</summary>
    public bool IsPlaying => State == PlaybackState.Playing;

    /// <summary>
    /// Завантажує WAV у пам'ять і починає відтворення.
    /// </summary>
    /// <param name="wavData">Вміст WAV-файлу.</param>
    /// <param name="voiceName">Ім'я голосу для відображення в плеєрі.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    public async Task PlayAsync(byte[] wavData, string? voiceName, CancellationToken cancellationToken = default)
    {
        VoiceName = voiceName;
        await _audioPlayer.PlayAsync(wavData, cancellationToken);
    }

    /// <summary>
    /// Запускає чергу фрагментів із пакетним відтворенням.
    /// </summary>
    /// <param name="fragments">Фрагменти в порядку відтворення.</param>
    /// <param name="voice">Ім'я голосу для відображення в плеєрі.</param>
    /// <param name="speed">Швидкість мовлення.</param>
    /// <param name="batchSize">Кількість фрагментів у пакеті.</param>
    /// <param name="pauseAfterEachBatch">Чи зупинятися після кожного пакета.</param>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fragments"/> дорівнює <see langword="null"/>.</exception>
    public async Task StartQueueAsync(
        IReadOnlyList<TextFragment> fragments,
        string? voice,
        double speed,
        int batchSize,
        bool pauseAfterEachBatch = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fragments);

        VoiceName = voice;
        HasQueue = true;
        IsBatchPending = false;
        ContinueButtonText = null;
        QueueStatusText = null;
        CurrentFragmentText = null;

        await _fragmentPlayback.StartAsync(
            fragments,
            new SpeechQueueOptions
            {
                Voice = voice,
                Speed = speed,
                BatchSize = batchSize,
                PauseAfterEachBatch = pauseAfterEachBatch
            },
            cancellationToken);
    }

    /// <summary>Призупиняє або продовжує відтворення залежно від поточного стану.</summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        if (IsBatchPending)
        {
            ContinueQueueCommand.Execute(null);
            return;
        }

        switch (_audioPlayer.State)
        {
            case PlaybackState.Playing:
                _audioPlayer.Pause();
                break;
            case PlaybackState.Paused:
                _audioPlayer.Resume();
                break;
            default:
                _logger.LogDebug("Немає активного потоку для паузи або продовження");
                break;
        }
    }

    /// <summary>Запускає наступний пакет фрагментів.</summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    [RelayCommand]
    private async Task ContinueQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _fragmentPlayback.ContinueAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Скасування під час закриття застосунку або зупинки черги.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося продовжити чергу фрагментів");
        }
    }

    /// <summary>Зупиняє відтворення або всю чергу фрагментів.</summary>
    [RelayCommand]
    private void Stop()
    {
        if (_fragmentPlayback.HasQueue || IsBatchPending)
        {
            _fragmentPlayback.Stop();
            ResetQueueState();
            return;
        }

        _audioPlayer.Stop();
    }

    /// <summary>
    /// Синхронізує стан плеєра з подією аудіосервісу (може надходити з потоку BASS).
    /// </summary>
    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        OnUiThread(() => State = state);
    }

    /// <summary>Оновлює лічильник після старту фрагмента.</summary>
    private void OnFragmentStarted(object? sender, FragmentStartedEventArgs e)
    {
        OnUiThread(() =>
        {
            HasQueue = true;
            IsBatchPending = false;
            ContinueButtonText = null;
            QueueStatusText = null;
            _lastTotalFragments = _fragmentPlayback.TotalFragments;
            _lastTotalBatches = e.TotalBatches;
            FragmentPositionText =
                $"Фрагмент {e.Fragment.Index} з {_lastTotalFragments} · пакет {e.Batch} з {e.TotalBatches}";
            CurrentFragmentText = BuildFragmentPreview(e.Fragment.Text);
        });
    }

    /// <summary>Показує стан очікування наступного пакета.</summary>
    private void OnBatchCompleted(object? sender, BatchCompletedEventArgs e)
    {
        OnUiThread(() =>
        {
            HasQueue = true;
            IsBatchPending = true;
            ContinueButtonText = e.HasNext ? $"Наступний пакет ({e.CompletedBatch + 1} з {e.TotalBatches})" : null;
            StateText = $"Пакет {e.CompletedBatch} з {e.TotalBatches} завершено";
            QueueStatusText = e.HasNext
                ? $"Пакет {e.CompletedBatch} з {e.TotalBatches} завершено. Натисніть ▶ або «Наступний пакет»"
                : null;
        });
    }

    /// <summary>Скидає стан після завершення всієї черги.</summary>
    private void OnQueueCompleted(object? sender, EventArgs e)
    {
        OnUiThread(() =>
        {
            HasQueue = false;
            IsBatchPending = false;
            ContinueButtonText = null;
            StateText = "Готово";
            QueueStatusText = _lastTotalFragments > 0
                ? $"Готово: {_lastTotalBatches} {PluralWord(_lastTotalBatches, "пакет", "пакети", "пакетів")}, " +
                  $"{_lastTotalFragments} {PluralWord(_lastTotalFragments, "фрагмент", "фрагменти", "фрагментів")}"
                : null;
        });
    }

    /// <summary>Показує помилку, через яку черга зупинилася.</summary>
    private void OnQueueFailed(object? sender, Exception e)
    {
        _logger.LogWarning(e, "Черга фрагментів зупинилася з помилкою");

        OnUiThread(() =>
        {
            HasQueue = false;
            IsBatchPending = false;
            ContinueButtonText = null;
            StateText = "Помилка аудіо";
            QueueStatusText = null;
            QueueFailed?.Invoke(this, e);
        });
    }

    /// <summary>Скидає лічильники черги після ручної зупинки.</summary>
    private void ResetQueueState()
    {
        HasQueue = false;
        IsBatchPending = false;
        ContinueButtonText = null;
        FragmentPositionText = null;
        CurrentFragmentText = null;
        QueueStatusText = null;
    }

    /// <summary>Формує короткий опис фрагмента для показу в UI.</summary>
    /// <param name="text">Текст фрагмента.</param>
    /// <returns>Перші 60 символів в один рядок; довший текст завершується трикрапкою.</returns>
    private static string BuildFragmentPreview(string text)
    {
        var normalized = text.ReplaceLineEndings(" ").Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized.Length <= FragmentPreviewLength
            ? normalized
            : normalized[..FragmentPreviewLength] + "…";
    }

    /// <summary>
    /// Оновлює похідні властивості після зміни стану.
    /// </summary>
    partial void OnStateChanged(PlaybackState value)
    {
        StateText = value switch
        {
            PlaybackState.Idle => "Готово",
            PlaybackState.Playing => "Відтворення",
            PlaybackState.Paused => "Пауза",
            PlaybackState.Stopped => "Зупинено",
            PlaybackState.Error => "Помилка аудіо",
            _ => "Готово"
        };

        PlayPauseGlyph = value == PlaybackState.Playing ? "⏸" : "▶";
        OnPropertyChanged(nameof(IsPlaying));
    }

    /// <summary>Застосовує зміну гучності до аудіосервісу.</summary>
    partial void OnVolumeChanged(double value) => _audioPlayer.SetVolume(value);

    /// <summary>Відписується від подій аудіосервісу та черги.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _audioPlayer.StateChanged -= OnPlayerStateChanged;
        _fragmentPlayback.FragmentStarted -= OnFragmentStarted;
        _fragmentPlayback.BatchCompleted -= OnBatchCompleted;
        _fragmentPlayback.QueueCompleted -= OnQueueCompleted;
        _fragmentPlayback.Failed -= OnQueueFailed;
    }
}
