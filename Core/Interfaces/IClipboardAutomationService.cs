#nullable enable

using System.Threading;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Captures text selected in another application.
/// </summary>
/// <remarks>
/// On Wayland, the selection of another window cannot be read directly, so the implementation
/// simulates <c>Ctrl+C</c> and reads the system clipboard.
/// </remarks>
public interface IClipboardAutomationService
{
    /// <summary>
    /// Copies the selected text (by simulating <c>Ctrl+C</c>) and reads it from the clipboard.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The captured text, or an empty string if nothing is selected or the clipboard is unavailable.
    /// </returns>
    Task<string> CaptureSelectedTextAsync(CancellationToken cancellationToken = default);
}
