#nullable enable

using System;
using System.Threading.Tasks;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Global hotkey that works regardless of the active window.
/// </summary>
public interface IGlobalHotkeyService
{
    /// <summary>Raised when the user presses the configured hotkey (<c>Alt+Q</c>).</summary>
    event EventHandler? HotkeyPressed;

    /// <summary>
    /// Starts intercepting keyboard input.
    /// </summary>
    /// <remarks>
    /// Repeated calls do nothing. If the hook cannot be started, the error is written to the log,
    /// and the application keeps running without the hotkey.
    /// </remarks>
    void Start();

    /// <summary>
    /// Stops keyboard interception and releases the hook's resources.
    /// </summary>
    Task StopAsync();
}
