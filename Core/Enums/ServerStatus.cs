#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Availability state of the TTS server.
/// </summary>
public enum ServerStatus
{
    /// <summary>The check has not been performed yet.</summary>
    Unknown,

    /// <summary>The server responds to requests.</summary>
    Online,

    /// <summary>The server does not respond (network, port, or the process is not running).</summary>
    Offline,

    /// <summary>The server responded, but with an error (invalid response, 5xx, etc.).</summary>
    Error
}
