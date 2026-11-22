#nullable enable

namespace UA_TTS_Client.Core.Enums;

/// <summary>
/// Lifecycle state of the packaged TTS server process as known to the client.
/// </summary>
/// <remarks>
/// This is about the process the client manages (started/stopped from the settings tab),
/// not about HTTP availability — that is <see cref="ServerStatus"/>.
/// </remarks>
public enum ServerProcessState
{
    /// <summary>No server process is running.</summary>
    NotRunning,

    /// <summary>The process was started and the server is still loading (model, voices).</summary>
    Starting,

    /// <summary>The server process is running and answers health checks.</summary>
    Running,

    /// <summary>The last start attempt failed (process died, timeout, missing executable).</summary>
    Failed
}
