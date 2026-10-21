#nullable enable

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
    private readonly ITtsApiClient _apiClient;
    private readonly IClipboardAutomationService _clipboard;
    private readonly SettingsViewModel _settings;
    private readonly PlaybackControlViewModel _playback;
    private readonly ILogger<QuickTtsViewModel> _logger;

    /// <summary>
    /// Створює ViewModel вкладки Quick TTS.
    /// </summary>
    /// <param name="apiClient">Клієнт TTS-сервера.</param>
    /// <param name="clipboard">Сервіс захоплення виділеного тексту.</param>
    /// <param name="settings">Налаштування (голос і швидкість).</param>
    /// <param name="playback">Міні-плеєр.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public QuickTtsViewModel(
        ITtsApiClient apiClient,
        IClipboardAutomationService clipboard,
        SettingsViewModel settings,
        PlaybackControlViewModel playback,
        ILogger<QuickTtsViewModel> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));
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

    /// <summary>
    /// Захоплює виділений текст із активного вікна.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    [RelayCommand(CanExecute = nameof(CanCapture))]
    private async Task CaptureSelectedTextAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;

        try
        {
            var captured = await _clipboard.CaptureSelectedTextAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(captured))
            {
                StatusMessage = "Нічого не захоплено — виділіть текст в активному вікні.";
                return;
            }

            Text = captured.Trim();
            StatusMessage = $"Захоплено {Text.Length} {SymbolsWord(Text.Length)}.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Захоплення скасовано.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не вдалося захопити виділений текст");
            ErrorMessage = "Не вдалося захопити виділений текст.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Синтезує мовлення та починає відтворення.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування.</param>
    [RelayCommand(CanExecute = nameof(CanSynthesize), IncludeCancelCommand = true)]
    private async Task SynthesizeAsync(CancellationToken cancellationToken)
    {
        var text = Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        ErrorMessage = null;
        IsBusy = true;
        StatusMessage = "Синтез…";

        try
        {
            var request = new TtsRequestDto
            {
                Text = text,
                Voice = _settings.SelectedVoice,
                Speed = _settings.Speed,
                Volume = 1.0,
                Translate = false
            };

            var wav = await _apiClient.SynthesizeAsync(request, cancellationToken);
            await _playback.PlayAsync(wav, _settings.SelectedVoice, cancellationToken);

            StatusMessage = $"Готово: {text.Length} {SymbolsWord(text.Length)}, {wav.Length / 1024} КБ аудіо.";
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

    private bool CanCapture() => !IsBusy;

    private bool CanSynthesize() => !IsBusy && !string.IsNullOrWhiteSpace(Text);

    /// <summary>Оновлює доступність команд після зміни тексту.</summary>
    partial void OnTextChanged(string value)
    {
        SynthesizeCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Оновлює доступність команд під час виконання операції.</summary>
    partial void OnIsBusyChanged(bool value)
    {
        CaptureSelectedTextCommand.NotifyCanExecuteChanged();
        SynthesizeCommand.NotifyCanExecuteChanged();
    }
}
