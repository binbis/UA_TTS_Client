#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using UA_TTS_Client.Core.Interfaces;

namespace UA_TTS_Client.Views.Services;

/// <summary>
/// Системний діалог збереження файлу на базі Avalonia <see cref="IStorageProvider"/>.
/// </summary>
/// <remarks>
/// Єдиний UI-специфічний сервіс застосунку: <c>Infrastructure</c> не повинна залежати
/// від Avalonia, тому діалог живе у <c>Views</c>. Викликати лише з UI-потоку.
/// </remarks>
public sealed class AvaloniaFileSaveDialogService : IFileSaveDialogService
{
    /// <inheritdoc />
    public async Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(suggestedFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(extension);
        cancellationToken.ThrowIfCancellationRequested();

        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop ||
            desktop.MainWindow?.StorageProvider is not { } storageProvider)
        {
            return null;
        }

        var normalizedExtension = extension.TrimStart('.');
        var options = new FilePickerSaveOptions
        {
            SuggestedFileName = suggestedFileName,
            DefaultExtension = normalizedExtension,
            ShowOverwritePrompt = true,
            FileTypeChoices = [CreateFileType(normalizedExtension)]
        };

        var file = await storageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    /// <summary>Створює тип файлу для фільтра діалогу.</summary>
    private static FilePickerFileType CreateFileType(string extension) => extension switch
    {
        "mp4" => new FilePickerFileType("Аудіо MP4")
        {
            Patterns = ["*.mp4"],
            MimeTypes = ["audio/mp4"]
        },
        "wav" => new FilePickerFileType("Аудіо WAV")
        {
            Patterns = ["*.wav"],
            MimeTypes = ["audio/wav"]
        },
        _ => new FilePickerFileType(extension.ToUpperInvariant())
        {
            Patterns = [$"*.{extension}"]
        }
    };
}
