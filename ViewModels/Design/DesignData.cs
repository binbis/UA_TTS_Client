#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;
using UA_TTS_Client.ViewModels.Components;
using UA_TTS_Client.ViewModels.Tabs;

namespace UA_TTS_Client.ViewModels.Design;

/// <summary>
/// Дані для прев'ю XAML у Rider.
/// </summary>
/// <remarks>
/// Використовується лише дизайнером: code-behind відповідного View ставить його як
/// <c>DataContext</c>, коли <c>Design.IsDesignMode</c> істинний. Під час роботи застосунку
/// всі ViewModel створюються через DI з реальними сервісами.
/// </remarks>
public static class DesignData
{
    private static readonly AppConfig PreviewConfig = new();

    /// <summary>Міні-плеєр із фіктивним аудіосервісом.</summary>
    public static PlaybackControlViewModel Playback { get; } =
        new(PreviewConfig, new PreviewAudioPlayer(), new PreviewFragmentPlaybackService(),
            NullLogger<PlaybackControlViewModel>.Instance);

    /// <summary>Вкладка налаштувань із тестовими голосами.</summary>
    public static SettingsViewModel Settings { get; } = CreateSettings();

    /// <summary>Вкладка Quick TTS із тестовим текстом.</summary>
    public static QuickTtsViewModel QuickTts { get; } =
        new(new PreviewClipboard(), new PreviewTextChunker(), Settings, Playback,
            new PreviewAudioExportService(), new PreviewFileSaveDialogService(),
            NullLogger<QuickTtsViewModel>.Instance);

    /// <summary>Головна ViewModel для прев'ю вікна.</summary>
    public static MainWindowViewModel MainWindow { get; } =
        new(QuickTts, Settings, Playback, new PreviewTtsApiClient(), new PreviewHotkeyService(),
            NullLogger<MainWindowViewModel>.Instance);

    private static SettingsViewModel CreateSettings()
    {
        var viewModel = new SettingsViewModel(
            new PreviewSettingsService(),
            new PreviewTtsApiClient(),
            Playback,
            PreviewConfig,
            NullLogger<SettingsViewModel>.Instance)
        {
            Voices = ["Вероніка Дорош", "Денис Денисенко"],
            SelectedVoice = "Вероніка Дорош",
            StatusMessage = "Дані для прев'ю"
        };

        return viewModel;
    }
}

/// <summary>Фіктивний аудіосервіс, який нічого не відтворює.</summary>
file sealed class PreviewAudioPlayer : IAudioPlayerService
{
    public PlaybackState State => PlaybackState.Idle;

    public double Volume => 1.0;

    public event EventHandler<PlaybackState>? StateChanged
    {
        add { }
        remove { }
    }

    public event EventHandler? TrackEnded
    {
        add { }
        remove { }
    }

    public Task PlayAsync(byte[] wavData, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Pause()
    {
    }

    public void Resume()
    {
    }

    public void Stop()
    {
    }

    public void SetVolume(double volume)
    {
    }
}

/// <summary>Фіктивна черга фрагментів для прев'ю.</summary>
file sealed class PreviewFragmentPlaybackService : IFragmentPlaybackService
{
    public event EventHandler<FragmentStartedEventArgs>? FragmentStarted
    {
        add { }
        remove { }
    }

    public event EventHandler<BatchCompletedEventArgs>? BatchCompleted
    {
        add { }
        remove { }
    }

    public event EventHandler? QueueCompleted
    {
        add { }
        remove { }
    }

    public event EventHandler<Exception>? Failed
    {
        add { }
        remove { }
    }

    public bool HasQueue => false;

    public bool IsBatchPending => false;

    public int CurrentFragment => 0;

    public int TotalFragments => 0;

    public int CurrentBatch => 0;

    public int TotalBatches => 0;

    public Task StartAsync(
        IReadOnlyList<TextFragment> fragments,
        SpeechQueueOptions options,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ContinueAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Pause()
    {
    }

    public void Resume()
    {
    }

    public void Stop()
    {
    }
}

/// <summary>Фіктивний поділ тексту на фрагменти для прев'ю.</summary>
file sealed class PreviewTextChunker : ITextChunker
{
    public IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options)
        => string.IsNullOrWhiteSpace(text) ? [] : [new TextFragment(1, text.Trim())];
}

/// <summary>Фіктивний клієнт API з двома голосами.</summary>
file sealed class PreviewTtsApiClient : ITtsApiClient
{
    private static readonly string[] PreviewVoices = ["Вероніка Дорош", "Денис Денисенко"];

    public Task<IReadOnlyList<string>> GetVoicesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<string>>(PreviewVoices);

    public Task<ServerHealth> GetHealthAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new ServerHealth { Status = "ok", Device = "cuda", Voices = PreviewVoices.Length });

    public Task<string> VerbalizeAsync(string text, CancellationToken cancellationToken = default)
        => Task.FromResult(text);

    public Task<byte[]> SynthesizeAsync(TtsRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<byte[]> ConvertAsync(byte[] wavData, string format, CancellationToken cancellationToken = default)
        => Task.FromResult(Array.Empty<byte>());

    public Task<string> ParseFileAsync(Stream fileStream, string fileName, CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}

/// <summary>Фіктивний сервіс експорту аудіо, який нічого не робить.</summary>
file sealed class PreviewAudioExportService : IAudioExportService
{
    public Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Фіктивний діалог збереження файлу (завжди скасовується).</summary>
file sealed class PreviewFileSaveDialogService : IFileSaveDialogService
{
    public Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

/// <summary>Фіктивне сховище налаштувань.</summary>
file sealed class PreviewSettingsService : ISettingsService
{
    public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppConfig());

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Фіктивний сервіс буфера обміну.</summary>
file sealed class PreviewClipboard : IClipboardAutomationService
{
    public Task<string> CaptureSelectedTextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}

/// <summary>Фіктивна гаряча клавіша.</summary>
file sealed class PreviewHotkeyService : IGlobalHotkeyService
{
    public event EventHandler? HotkeyPressed
    {
        add { }
        remove { }
    }

    public void Start()
    {
    }

    public Task StopAsync() => Task.CompletedTask;
}
