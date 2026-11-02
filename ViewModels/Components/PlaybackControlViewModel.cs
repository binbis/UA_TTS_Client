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
/// Mini player: controls playback of synthesized audio and shows its state.
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
    /// Creates the mini player.
    /// </summary>
    /// <param name="config">Startup settings (the initial volume is taken from there).</param>
    /// <param name="audioPlayer">Playback service.</param>
    /// <param name="fragmentPlayback">Fragment queue.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
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

        // The setter applies the volume to BASS immediately.
        Volume = Math.Clamp(config.Volume, 0d, 1d);
    }

    /// <summary>Current playback state.</summary>
    [ObservableProperty]
    public partial PlaybackState State { get; set; } = PlaybackState.Idle;

    /// <summary>Human-readable description of the state for the status bar.</summary>
    [ObservableProperty]
    public partial string StateText { get; set; } = "Готово";

    /// <summary>Symbol on the pause/resume button.</summary>
    [ObservableProperty]
    public partial string PlayPauseGlyph { get; set; } = "▶";

    /// <summary>Name of the voice used to speak the current track.</summary>
    [ObservableProperty]
    public partial string? VoiceName { get; set; }

    /// <summary>Playback volume in the range from 0.0 to 1.0.</summary>
    [ObservableProperty]
    public partial double Volume { get; set; } = 1.0;

    /// <summary>Queue position: "Фрагмент 2 з 5 · пакет 1 з 3" (Fragment 2 of 5 · batch 1 of 3).</summary>
    [ObservableProperty]
    public partial string? FragmentPositionText { get; set; }

    /// <summary>Beginning of the current fragment's text (up to 60 characters).</summary>
    [ObservableProperty]
    public partial string? CurrentFragmentText { get; set; }

    /// <summary>Queue state for the Quick TTS tab: completion of a batch or of the whole queue.</summary>
    [ObservableProperty]
    public partial string? QueueStatusText { get; set; }

    /// <summary>Raised on the UI thread when the fragment queue stops because of an error.</summary>
    public event EventHandler<Exception>? QueueFailed;

    /// <summary>Whether a batch has completed and the queue is waiting to continue.</summary>
    [ObservableProperty]
    public partial bool IsBatchPending { get; set; }

    /// <summary>Text of the button that continues with the next batch.</summary>
    [ObservableProperty]
    public partial string? ContinueButtonText { get; set; }

    /// <summary>Whether an active fragment queue exists.</summary>
    [ObservableProperty]
    public partial bool HasQueue { get; set; }

    /// <summary>Flag indicating that audio is currently playing.</summary>
    public bool IsPlaying => State == PlaybackState.Playing;

    /// <summary>
    /// Loads WAV data into memory and starts playback.
    /// </summary>
    /// <param name="wavData">WAV file contents.</param>
    /// <param name="voiceName">Voice name shown in the player.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task PlayAsync(byte[] wavData, string? voiceName, CancellationToken cancellationToken = default)
    {
        VoiceName = voiceName;
        await _audioPlayer.PlayAsync(wavData, cancellationToken);
    }

    /// <summary>
    /// Starts a fragment queue with batch playback.
    /// </summary>
    /// <param name="fragments">Fragments in playback order.</param>
    /// <param name="voice">Voice name shown in the player.</param>
    /// <param name="speed">Speech speed.</param>
    /// <param name="batchSize">Number of fragments per batch.</param>
    /// <param name="pauseAfterEachBatch">Whether to stop after each batch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentNullException"><paramref name="fragments"/> is <see langword="null"/>.</exception>
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

    /// <summary>Pauses or resumes playback depending on the current state.</summary>
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

    /// <summary>Starts the next batch of fragments.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand]
    private async Task ContinueQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _fragmentPlayback.ContinueAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation during application shutdown or queue stop.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не вдалося продовжити чергу фрагментів");
        }
    }

    /// <summary>Stops playback or the whole fragment queue.</summary>
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
    /// Synchronizes the player state with the audio service event (may arrive from the BASS thread).
    /// </summary>
    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        OnUiThread(() => State = state);
    }

    /// <summary>Updates the counter after a fragment starts.</summary>
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

    /// <summary>Shows the waiting state for the next batch.</summary>
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

    /// <summary>Resets the state after the whole queue has completed.</summary>
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

    /// <summary>Shows the error that stopped the queue.</summary>
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

    /// <summary>Resets the queue counters after a manual stop.</summary>
    private void ResetQueueState()
    {
        HasQueue = false;
        IsBatchPending = false;
        ContinueButtonText = null;
        FragmentPositionText = null;
        CurrentFragmentText = null;
        QueueStatusText = null;
    }

    /// <summary>Builds a short description of the fragment for display in the UI.</summary>
    /// <param name="text">Fragment text.</param>
    /// <returns>The first 60 characters on one line; longer text ends with an ellipsis.</returns>
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
    /// Updates the derived properties after a state change.
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

    /// <summary>Applies the volume change to the audio service.</summary>
    partial void OnVolumeChanged(double value) => _audioPlayer.SetVolume(value);

    /// <summary>Unsubscribes from the audio service and queue events.</summary>
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
