#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Exceptions;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;
using UA_TTS_Client.ViewModels.Components;

namespace UA_TTS_Client.ViewModels.Tabs;

/// <summary>
/// Quick TTS tab: captures selected text, synthesizes speech, shows queue status,
/// and passes audio to the mini player.
/// </summary>
public partial class QuickTtsViewModel : ViewModelBase, IDisposable
{
    private readonly IClipboardAutomationService _clipboard;
    private readonly ITextChunker _textChunker;
    private readonly SettingsViewModel _settings;
    private readonly PlaybackControlViewModel _playback;
    private readonly IAudioExportService _audioExport;
    private readonly IFileSaveDialogService _fileSaveDialog;
    private readonly ILogger<QuickTtsViewModel> _logger;
    private bool _disposed;

    /// <summary>
    /// Creates the view model for the Quick TTS tab.
    /// </summary>
    /// <param name="clipboard">Service that captures selected text.</param>
    /// <param name="textChunker">Service that splits text into fragments.</param>
    /// <param name="settings">Settings (voice, speed, chunking mode).</param>
    /// <param name="playback">Mini player and fragment queue.</param>
    /// <param name="audioExport">Service that exports audio to a file.</param>
    /// <param name="fileSaveDialog">System file save dialog.</param>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException">One of the arguments is <see langword="null"/>.</exception>
    public QuickTtsViewModel(
        IClipboardAutomationService clipboard,
        ITextChunker textChunker,
        SettingsViewModel settings,
        PlaybackControlViewModel playback,
        IAudioExportService audioExport,
        IFileSaveDialogService fileSaveDialog,
        ILogger<QuickTtsViewModel> logger)
    {
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _textChunker = textChunker ?? throw new ArgumentNullException(nameof(textChunker));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
        _audioExport = audioExport ?? throw new ArgumentNullException(nameof(audioExport));
        _fileSaveDialog = fileSaveDialog ?? throw new ArgumentNullException(nameof(fileSaveDialog));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _playback.QueueFailed += OnPlaybackQueueFailed;
    }

    /// <summary>Mini player with the fragment queue (for XAML bindings).</summary>
    public PlaybackControlViewModel Playback => _playback;

    /// <summary>Text to speak.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Current operation status.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Error text shown to the user.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Indicates that a long-running operation is in progress.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Indicates that audio is being exported to a file.</summary>
    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    /// <summary>Export progress text.</summary>
    [ObservableProperty]
    public partial string? ExportProgressText { get; set; }

    /// <summary>Export progress in percent (from 0 to 100).</summary>
    [ObservableProperty]
    public partial double ExportProgressPercent { get; set; }

    /// <summary>
    /// Captures the selected text from the active window (the "Захопити виділене" (Capture selection) button).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task CaptureSelectedTextAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;

        try
        {
            await CaptureSelectedTextCoreAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Captures the selected text for the <c>Alt+Q</c> hotkey.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the text was captured and written to <see cref="Text"/>.</returns>
    /// <remarks>
    /// Called before the window is activated: the simulated <c>Ctrl+C</c> must reach the active window of another application,
    /// otherwise nothing will be copied. A repeated call while work is in progress is ignored.
    /// </remarks>
    public async Task<bool> CaptureForHotkeyAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy || IsExporting)
        {
            _logger.LogDebug("Захоплення за гарячою клавішею пропущено: вже виконується інша операція");
            return false;
        }

        IsBusy = true;

        try
        {
            return await CaptureSelectedTextCoreAsync(cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Shared logic for capturing selected text for the button and the hotkey.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><see langword="true"/> if the text was captured and written to <see cref="Text"/>.</returns>
    private async Task<bool> CaptureSelectedTextCoreAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;

        try
        {
            var captured = await _clipboard.CaptureSelectedTextAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(captured))
            {
                StatusMessage = "Нічого не захоплено — виділіть текст в активному вікні.";
                return false;
            }

            Text = captured.Trim();
            StatusMessage = $"Захоплено {Text.Length} {SymbolsWord(Text.Length)}.";
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Захоплення скасовано.";
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося захопити виділений текст");
            ErrorMessage = "Не вдалося захопити виділений текст.";
            return false;
        }
    }

    /// <summary>
    /// Synthesizes speech and starts playback (the "Озвучити" (Speak) button).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    [RelayCommand(CanExecute = nameof(CanSynthesize), IncludeCancelCommand = true)]
    private Task SynthesizeAsync(CancellationToken cancellationToken) =>
        SynthesizeCurrentTextAsync(cancellationToken);

    /// <summary>
    /// Synthesizes the current text and starts the playback queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Shared logic for the "Озвучити" (Speak) command and auto-synthesis after <c>Alt+Q</c>.
    /// </remarks>
    public async Task SynthesizeCurrentTextAsync(CancellationToken cancellationToken = default)
    {
        var text = Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var fragments = _textChunker.Split(text, new TextChunkingOptions
        {
            Mode = _settings.ChunkingMode
        });

        if (fragments.Count == 0)
        {
            StatusMessage = "Немає тексту для озвучення.";
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        StatusMessage =
            $"Підготовлено {fragments.Count} {PluralWord(fragments.Count, "фрагмент", "фрагменти", "фрагментів")}…";

        try
        {
            var batchSize = Math.Clamp(
                _settings.BatchSize,
                SpeechQueueOptions.MinBatchSize,
                SpeechQueueOptions.MaxBatchSize);
            var totalBatches = (int)Math.Ceiling(fragments.Count / (double)batchSize);

            await _playback.StartQueueAsync(
                fragments,
                _settings.SelectedVoice,
                _settings.Speed,
                batchSize,
                _settings.PauseAfterEachBatch,
                cancellationToken);

            StatusMessage =
                $"Підготовлено {fragments.Count} {PluralWord(fragments.Count, "фрагмент", "фрагменти", "фрагментів")} · пакет 1 з {totalBatches}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Синтез скасовано.";
        }
        catch (TtsApiException ex)
        {
            _logger.LogWarning(ex, "Помилка синтезу");
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка синтезу");
            ErrorMessage = "Не вдалося озвучити текст.";
            StatusMessage = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Saves the entire current text to an audio file (MP4 or WAV).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// The format is taken from the settings; the user chooses the path in the system dialog.
    /// Current playback is stopped before the export so that synthesis does not compete
    /// for the GPU with the fragment queue.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExport), IncludeCancelCommand = true)]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        var text = Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var format = _settings.ExportFormat;
        var extension = format == AudioExportFormat.Wav ? "wav" : "mp4";

        ErrorMessage = null;
        StatusMessage = null;

        string? destinationPath;
        try
        {
            destinationPath = await _fileSaveDialog.PickSavePathAsync(
                $"UA_TTS_{DateTime.Now:yyyy-MM-dd_HH-mm}",
                extension,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося відкрити діалог збереження файлу");
            ErrorMessage = "Не вдалося відкрити діалог збереження файлу.";
            return;
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            // The user cancelled the dialog — nothing is changed.
            return;
        }

        _playback.StopCommand.Execute(null);

        IsExporting = true;
        ExportProgressPercent = 0;
        ExportProgressText = "Експорт: підготовка…";

        try
        {
            var progress = new Progress<AudioExportProgress>(OnExportProgress);
            await _audioExport.ExportAsync(
                new AudioExportRequest
                {
                    Text = text,
                    Voice = _settings.SelectedVoice,
                    Speed = _settings.Speed,
                    Chunking = new TextChunkingOptions { Mode = _settings.ChunkingMode },
                    Format = format,
                    DestinationPath = destinationPath
                },
                progress,
                cancellationToken);

            StatusMessage = $"Збережено: {destinationPath}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Експорт скасовано.";
        }
        catch (TtsApiException ex)
        {
            _logger.LogWarning(ex, "Помилка експорту аудіо");
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _logger.LogError(ex, "Не вдалося зберегти файл");
            ErrorMessage = $"Не вдалося зберегти файл: {ex.Message}";
            StatusMessage = null;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Некоректні дані для експорту");
            ErrorMessage = ex.Message;
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Непередбачена помилка експорту аудіо");
            ErrorMessage = "Не вдалося зберегти аудіо.";
            StatusMessage = null;
        }
        finally
        {
            IsExporting = false;
            ExportProgressText = null;
        }
    }

    /// <summary>Updates the status and the progress bar during export.</summary>
    private void OnExportProgress(AudioExportProgress progress)
    {
        ExportProgressPercent = progress.TotalFragments > 0
            ? Math.Clamp(progress.CompletedFragments * 100d / progress.TotalFragments, 0d, 100d)
            : 0d;

        ExportProgressText = progress.Stage switch
        {
            AudioExportStage.Synthesizing =>
                $"Експорт: синтез {Math.Min(progress.CompletedFragments + 1, progress.TotalFragments)} із {progress.TotalFragments}…",
            AudioExportStage.Concatenating => "Експорт: склеювання…",
            AudioExportStage.Converting => "Експорт: конвертація в MP4…",
            AudioExportStage.Writing => "Експорт: запис файлу…",
            _ => "Експорт…"
        };
    }

    /// <summary>Shows a playback queue error in the Quick TTS status.</summary>
    private void OnPlaybackQueueFailed(object? sender, Exception exception)
    {
        _logger.LogWarning(exception, "Черга відтворення зупинилася з помилкою");
        StatusMessage = null;
        ErrorMessage = "Черга відтворення зупинилася з помилкою. Деталі — у журналі застосунку.";
    }

    private bool CanCapture() => !IsBusy && !IsExporting;

    private bool CanSynthesize() => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text);

    private bool CanExport() => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Updates command availability after the text changes.</summary>
    partial void OnTextChanged(string value)
    {
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Updates command availability while an operation is running.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        CaptureSelectedTextCommand.NotifyCanExecuteChanged();
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Updates command availability during export.</summary>
    partial void OnIsExportingChanged(bool value)
    {
        CaptureSelectedTextCommand.NotifyCanExecuteChanged();
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Unsubscribes from the mini player events.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _playback.QueueFailed -= OnPlaybackQueueFailed;
    }
}
