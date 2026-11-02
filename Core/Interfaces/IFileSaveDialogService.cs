#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Selects a file save path through the system dialog.
/// </summary>
/// <remarks>
/// The interface lives in <c>Core</c> so that the ViewModel does not depend on Avalonia; the implementation is
/// the only UI-specific service in <c>Views/Services</c>.
/// </remarks>
public interface IFileSaveDialogService
{
    /// <summary>
    /// Shows the file save dialog.
    /// </summary>
    /// <param name="suggestedFileName">File name without the extension.</param>
    /// <param name="extension">Extension without the dot: <c>mp4</c> or <c>wav</c>.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The local path, or <see langword="null"/> if the user canceled the dialog.</returns>
    /// <exception cref="ArgumentException">One of the string arguments is empty.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled before the dialog was opened.</exception>
    Task<string?> PickSavePathAsync(
        string suggestedFileName,
        string extension,
        CancellationToken cancellationToken = default);
}
