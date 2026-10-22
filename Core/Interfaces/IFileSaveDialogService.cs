#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Вибір шляху збереження файлу через системний діалог.
/// </summary>
/// <remarks>
/// Інтерфейс живе в <c>Core</c>, щоб ViewModel не залежала від Avalonia; реалізація —
/// єдиний UI-специфічний сервіс у <c>Views/Services</c>.
/// </remarks>
public interface IFileSaveDialogService
{
    /// <summary>
    /// Показує діалог збереження файлу.
    /// </summary>
    /// <param name="suggestedFileName">Ім'я файлу без розширення.</param>
    /// <param name="extension">Розширення без крапки: <c>mp4</c> або <c>wav</c>.</param>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>Локальний шлях або <see langword="null"/>, якщо користувач скасував діалог.</returns>
    /// <exception cref="ArgumentException">Один із рядкових аргументів порожній.</exception>
    /// <exception cref="OperationCanceledException">Операцію скасовано до відкриття діалогу.</exception>
    Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default);
}
