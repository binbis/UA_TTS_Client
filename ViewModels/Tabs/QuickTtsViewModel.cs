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
/// Вкладка Quick TTS: захоплення виділеного тексту, синтез і передача аудіо в міні-плеєр.
/// </summary>
public partial class QuickTtsViewModel : ViewModelBase
{
    private readonly IClipboardAutomationService _clipboard;
    private readonly ITextChunker _textChunker;
    private readonly SettingsViewModel _settings;
    private readonly PlaybackControlViewModel _playback;
    private readonly IAudioExportService _audioExport;
    private readonly IFileSaveDialogService _fileSaveDialog;
    private readonly ILogger<QuickTtsViewModel> _logger;

    /// <summary>
    /// Створює ViewModel вкладки Quick TTS.
    /// </summary>
    /// <param name="clipboard">Сервіс захоплення виділеного тексту.</param>
    /// <param name="textChunker">Сервіс поділу тексту на фрагменти.</param>
    /// <param name="settings">Налаштування (голос, швидкість, режим фрагментації).</param>
    /// <param name="playback">Міні-плеєр і черга фрагментів.</param>
    /// <param name="audioExport">Сервіс експорту аудіо у файл.</param>
    /// <param name="fileSaveDialog">Системний діалог збереження файлу.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
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
    }

    /// <summary>Текст для озвучення.</summary>
    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Поточний статус операції.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Текст помилки для показу користувачу.</summary>
    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Ознака виконання тривалої операції.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Ознака виконання експорту аудіо у файл.</summary>
    [ObservableProperty]
    public partial bool IsExporting { get; set; }

    /// <summary>Текст прогресу експорту.</summary>
    [ObservableProperty]
    public partial string? ExportProgressText { get; set; }

    /// <summary>Прогрес експорту у відсотках (від 0 до 100).</summary>
    [ObservableProperty]
    public partial double ExportProgressPercent { get; set; }

    /// <summary>
    /// Захоплює виділений текст із активного вікна (кнопка «Захопити виділене»).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
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
    /// Захоплює виділений текст для гарячої клавіші <c>Alt+Q</c>.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns><see langword="true"/>, якщо текст захоплено й записано у <see cref="Text"/>.</returns>
    /// <remarks>
    /// Викликається до активації вікна: симуляція <c>Ctrl+C</c> має піти в активне чуже вікно,
    /// інакше буде скопійовано порожньо. Повторний виклик під час роботи ігнорується.
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
    /// Спільна логіка захоплення виділеного тексту для кнопки та гарячої клавіші.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <returns><see langword="true"/>, якщо текст захоплено й записано у <see cref="Text"/>.</returns>
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
    /// Синтезує мовлення та починає відтворення (кнопка «Озвучити»).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    [RelayCommand(CanExecute = nameof(CanSynthesize), IncludeCancelCommand = true)]
    private Task SynthesizeAsync(CancellationToken cancellationToken) =>
        SynthesizeCurrentTextAsync(cancellationToken);

    /// <summary>
    /// Синтезує поточний текст і запускає чергу відтворення.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <remarks>
    /// Спільна логіка для команди «Озвучити» та автосинтезу після <c>Alt+Q</c>.
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
            Mode = _settings.SelectedChunkingMode
        });

        if (fragments.Count == 0)
        {
            StatusMessage = "Немає тексту для озвучення.";
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        StatusMessage = $"Підготовлено {fragments.Count} фрагментів…";

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
                cancellationToken);

            StatusMessage = $"Відтворення: {fragments.Count} фрагментів · пакет 1 з {totalBatches}.";
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
    /// Зберігає весь поточний текст в аудіофайл (MP4 або WAV).
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    /// <remarks>
    /// Формат береться з налаштувань; шлях користувач вибирає в системному діалозі.
    /// Перед експортом поточне відтворення зупиняється, щоб синтез не конкурував
    /// за GPU із чергою фрагментів.
    /// </remarks>
    [RelayCommand(CanExecute = nameof(CanExport), IncludeCancelCommand = true)]
    private async Task ExportAsync(CancellationToken cancellationToken)
    {
        var text = Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        var format = _settings.SelectedExportFormat;
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
            // Користувач скасував діалог — нічого не змінюємо.
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
                    Chunking = new TextChunkingOptions { Mode = _settings.SelectedChunkingMode },
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

    /// <summary>Оновлює статус і смугу прогресу під час експорту.</summary>
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

    private bool CanCapture() => !IsBusy && !IsExporting;

    private bool CanSynthesize() => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text);

    private bool CanExport() => !IsBusy && !IsExporting && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Оновлює доступність команд після зміни тексту.</summary>
    partial void OnTextChanged(string value)
    {
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Оновлює доступність команд під час виконання операції.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        CaptureSelectedTextCommand.NotifyCanExecuteChanged();
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Оновлює доступність команд під час експорту.</summary>
    partial void OnIsExportingChanged(bool value)
    {
        CaptureSelectedTextCommand.NotifyCanExecuteChanged();
        SynthesizeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
    }
}
