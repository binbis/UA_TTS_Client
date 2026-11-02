#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Audio playback state in the application.
/// </summary>
/// <remarks>
/// This is a custom domain enum: it deliberately does not depend on <c>ManagedBass.PlaybackState</c>,
/// so that the <c>Core</c> layer stays free of external libraries.
/// </remarks>
public enum PlaybackState
{
    /// <summary>Nothing is loaded; playback has not started yet.</summary>
    Idle,

    /// <summary>Audio is playing.</summary>
    Playing,

    /// <summary>Playback is paused; the position is kept.</summary>
    Paused,

    /// <summary>Playback was stopped by the user or the track was played to the end.</summary>
    Stopped,

    /// <summary>An audio subsystem error occurred (for example, the device could not be initialized).</summary>
    Error
}
