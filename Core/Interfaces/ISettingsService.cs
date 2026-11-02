#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Core.Interfaces;

/// <summary>
/// Saves and loads the application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads the settings from disk.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>
    /// The saved config, or a config with default values if the file is missing or corrupted.
    /// </returns>
    Task<AppConfig> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically saves the settings to disk.
    /// </summary>
    /// <param name="config">Configuration to save.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <exception cref="ArgumentNullException"><paramref name="config"/> is <see langword="null"/>.</exception>
    /// <exception cref="IOException">The file could not be written.</exception>
    Task SaveAsync(AppConfig config, CancellationToken cancellationToken = default);
}
