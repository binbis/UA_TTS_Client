#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Enums;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Starts and stops the packaged TTS server (<c>UA_TTS_Server</c> executable) next to the client.
/// </summary>
/// <remarks>
/// The server is managed manually only: there is no auto-start and no start on <c>Alt+Q</c>.
/// The server writes its own log file (<see cref="LogFilePath"/>); the client never redirects
/// the server output into it (that would duplicate the lines). If the server is already running
/// (started by hand), the service adopts it and never kills it.
/// </remarks>
public interface IServerProcessService
{
    /// <summary>Current lifecycle state of the server process.</summary>
    ServerProcessState State { get; }

    /// <summary>Whether the running server was started by this client (and may be stopped by it).</summary>
    bool IsStartedByClient { get; }

    /// <summary>Full path of the server log file (<c>UA_TTS_Server/logs/server.log</c> next to the client).</summary>
    string LogFilePath { get; }

    /// <summary>Default executable path: <c>UA_TTS_Server/UA_TTS_Server(.exe)</c> next to the client.</summary>
    string GetDefaultExecutablePath();

    /// <summary>Raised when <see cref="State"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Starts the server and waits until <c>GET /api/health</c> answers. Returns immediately
    /// (adopting the server) if a server already answers on the configured address.
    /// </summary>
    /// <param name="executablePath">Explicit executable path, or <see langword="null"/> for the default location.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="FileNotFoundException">The executable was not found.</exception>
    /// <exception cref="InvalidOperationException">The process exited before the server became ready.</exception>
    /// <exception cref="TimeoutException">The server did not become ready in time.</exception>
    Task StartAsync(string? executablePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the server process started by this client. A server started by hand is never killed.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">The running server was started by hand, not by the client.</exception>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Reads the last lines of the server log file (empty if the file does not exist yet).</summary>
    /// <param name="lineCount">How many lines to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<string>> ReadLogTailAsync(int lineCount, CancellationToken cancellationToken = default);
}
