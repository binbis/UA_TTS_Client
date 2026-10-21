#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Захоплення тексту, виділеного в іншому застосунку.
/// </summary>
/// <remarks>
/// На Wayland неможливо прочитати виділення чужого вікна напряму, тому реалізація
/// симулює <c>Ctrl+C</c> і читає системний буфер обміну.
/// </remarks>
public interface IClipboardAutomationService
{
    /// <summary>
    /// Копіює виділений текст (симуляцією <c>Ctrl+C</c>) і читає його з буфера обміну.
    /// </summary>
    /// <param name="cancellationToken">Токен скасування операції.</param>
    /// <returns>
    /// Захоплений текст або порожній рядок, якщо нічого не виділено чи буфер недоступний.
    /// </returns>
    Task<string> CaptureSelectedTextAsync(CancellationToken cancellationToken = default);
}
