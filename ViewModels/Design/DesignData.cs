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
/// Preview data for XAML in Rider.
/// </summary>
/// <remarks>
/// Used only by the designer: the code-behind of the corresponding View sets it as the
/// <c>DataContext</c> when <c>Design.IsDesignMode</c> is true. While the application runs,
/// all ViewModels are created through DI with real services.
/// </remarks>
public static class DesignData
{
    private static readonly AppConfig PreviewConfig = new();

    /// <summary>Mini player with a fake audio service.</summary>
    public static PlaybackControlViewModel Playback { get; } =
        new(PreviewConfig, new PreviewAudioPlayer(), new PreviewFragmentPlaybackService(),
            NullLogger<PlaybackControlViewModel>.Instance);

    /// <summary>Settings tab with test voices.</summary>
    public static SettingsViewModel Settings { get; } = CreateSettings();

    /// <summary>Quick TTS tab with test text.</summary>
    public static QuickTtsViewModel QuickTts { get; } =
        new(new PreviewClipboard(), new PreviewTextChunker(), Settings, Playback,
            new PreviewAudioExportService(), new PreviewFileSaveDialogService(),
            NullLogger<QuickTtsViewModel>.Instance);

    /// <summary>Main window ViewModel for the window preview.</summary>
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

/// <summary>Fake audio service that plays nothing.</summary>
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

/// <summary>Fake fragment queue for previews.</summary>
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

/// <summary>Fake text chunker for previews.</summary>
file sealed class PreviewTextChunker : ITextChunker
{
    public IReadOnlyList<TextFragment> Split(string text, TextChunkingOptions options)
        => string.IsNullOrWhiteSpace(text) ? [] : [new TextFragment(1, text.Trim())];
}

/// <summary>Fake API client with two voices.</summary>
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

/// <summary>Fake audio export service that does nothing.</summary>
file sealed class PreviewAudioExportService : IAudioExportService
{
    public Task ExportAsync(
        AudioExportRequest request,
        IProgress<AudioExportProgress>? progress = null,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}

/// <summary>Fake file save dialog (always cancelled).</summary>
file sealed class PreviewFileSaveDialogService : IFileSaveDialogService
{
    public Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
}

/// <summary>Fake settings storage.</summary>
file sealed class PreviewSettingsService : ISettingsService
{
    public Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new AppConfig());

    public Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

/// <summary>Fake clipboard service.</summary>
file sealed class PreviewClipboard : IClipboardAutomationService
{
    public Task<string> CaptureSelectedTextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(string.Empty);
}

/// <summary>Fake hotkey.</summary>
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
