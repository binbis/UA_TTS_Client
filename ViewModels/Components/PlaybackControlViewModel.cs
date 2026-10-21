#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
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
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ILogger<PlaybackControlViewModel> _logger;
    private bool _disposed;

    /// <summary>
    /// Створює міні-плеєр.
    /// </summary>
    /// <param name="config">Стартові налаштування (звідти береться початкова гучність).</param>
    /// <param name="audioPlayer">Сервіс відтворення.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public PlaybackControlViewModel(
        AppConfig config,
        IAudioPlayerService audioPlayer,
        ILogger<PlaybackControlViewModel> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _audioPlayer.StateChanged += OnPlayerStateChanged;

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

    /// <summary>Призупиняє або продовжує відтворення залежно від поточного стану.</summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
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

    /// <summary>Зупиняє відтворення.</summary>
    [RelayCommand]
    private void Stop() => _audioPlayer.Stop();

    /// <summary>
    /// Синхронізує стан плеєра з подією аудіосервісу (може надходити з потоку BASS).
    /// </summary>
    private void OnPlayerStateChanged(object? sender, PlaybackState state)
    {
        OnUiThread(() => State = state);
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

    /// <summary>Відписується від подій аудіосервісу.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _audioPlayer.StateChanged -= OnPlayerStateChanged;
    }
}
