#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;
using ManagedBass;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Interfaces;
using PlaybackState = UA_TTS_Client.Core.Enums.PlaybackState;

namespace UA_TTS_Client.Infrastructure.Audio;

/// <summary>
/// Plays WAV audio from memory via BASS (ManagedBass 4.0.2).
/// </summary>
/// <remarks>
/// <para>The <c>byte[]</c> array holding the WAV is kept in a field until playback ends: BASS reads
/// it directly, so garbage collection must not free the buffer.</para>
/// <para>Audio is never written to disk — everything happens in memory.</para>
/// </remarks>
public sealed class BassAudioPlayerService : IAudioPlayerService, IDisposable
{
    private const int DefaultOutputFrequency = 44100;

    private readonly ILogger<BassAudioPlayerService> _logger;
    private readonly object _sync = new();
    private readonly SyncProcedure _endSyncProcedure;

    private int _streamHandle;
    private byte[]? _wavBuffer;
    private bool _initialized;
    private bool _ownsBassInitialization;
    private bool _disposed;
    private double _volume = 1.0;
    private volatile PlaybackState _state = PlaybackState.Idle;

    /// <summary>
    /// Creates the playback service.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
    public BassAudioPlayerService(ILogger<BassAudioPlayerService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _endSyncProcedure = OnStreamEnded;
    }

    /// <inheritdoc />
    public PlaybackState State => _state;

    /// <inheritdoc />
    public double Volume => _volume;

    /// <inheritdoc />
    public event EventHandler<PlaybackState>? StateChanged;

    /// <inheritdoc />
    public event EventHandler? TrackEnded;

    /// <inheritdoc />
    public Task PlayAsync(byte[] wavData, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(wavData);
        if (wavData.Length == 0)
        {
            throw new ArgumentException("WAV-дані порожні.", nameof(wavData));
        }

        cancellationToken.ThrowIfCancellationRequested();

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!EnsureInitialized())
            {
                return Task.CompletedTask;
            }

            StopCore();

            var handle = Bass.CreateStream(wavData, 0, wavData.Length, BassFlags.Default);
            if (handle == 0)
            {
                var error = Bass.LastError;
                _logger.LogError("Не вдалося створити аудіопотік: {Error}", error);
                SetState(PlaybackState.Error);
                return Task.CompletedTask;
            }

            _streamHandle = handle;
            _wavBuffer = wavData;

            // The "end of track" sync is required for the automatic transition to Stopped.
            if (Bass.ChannelSetSync(handle, SyncFlags.End, 0, _endSyncProcedure, IntPtr.Zero) == 0)
            {
                _logger.LogWarning("Не вдалося встановити синхронізацію завершення треку: {Error}", Bass.LastError);
            }
            Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, (float)_volume);

            // restart: true — start from the beginning, even if the stream was just created.
            if (!Bass.ChannelPlay(handle, true))
            {
                var error = Bass.LastError;
                _logger.LogError("Не вдалося почати відтворення: {Error}", error);
                ReleaseStream();
                SetState(PlaybackState.Error);
                return Task.CompletedTask;
            }

            _logger.LogDebug("Відтворення почато: {Bytes} байт WAV", wavData.Length);
            SetState(PlaybackState.Playing);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (_sync)
        {
            if (_disposed || _streamHandle == 0 || _state != PlaybackState.Playing)
            {
                return;
            }

            if (Bass.ChannelPause(_streamHandle))
            {
                SetState(PlaybackState.Paused);
            }
            else
            {
                _logger.LogWarning("Не вдалося призупинити відтворення: {Error}", Bass.LastError);
            }
        }
    }

    /// <inheritdoc />
    public void Resume()
    {
        lock (_sync)
        {
            if (_disposed || _streamHandle == 0 || _state != PlaybackState.Paused)
            {
                return;
            }

            if (Bass.ChannelPlay(_streamHandle, false))
            {
                SetState(PlaybackState.Playing);
            }
            else
            {
                _logger.LogWarning("Не вдалося продовжити відтворення: {Error}", Bass.LastError);
            }
        }
    }

    /// <inheritdoc />
    public void Stop()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            var hadStream = _streamHandle != 0;
            StopCore();

            if (hadStream)
            {
                SetState(PlaybackState.Stopped);
            }
        }
    }

    /// <inheritdoc />
    public void SetVolume(double volume)
    {
        if (double.IsNaN(volume))
        {
            throw new ArgumentException("Гучність не може бути NaN.", nameof(volume));
        }

        var clamped = Math.Clamp(volume, 0d, 1d);

        lock (_sync)
        {
            _volume = clamped;

            if (_streamHandle != 0 &&
                !Bass.ChannelSetAttribute(_streamHandle, ChannelAttribute.Volume, (float)clamped))
            {
                _logger.LogWarning("Не вдалося встановити гучність: {Error}", Bass.LastError);
            }
        }
    }

    /// <summary>
    /// Initializes BASS once for the lifetime of the service.
    /// </summary>
    /// <returns><see langword="true"/> if the device is ready for playback.</returns>
    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return true;
        }

        try
        {
            // On Linux, index 0 is not the "default device" but the virtual
            // "No sound" device. Bass.DefaultDevice (-1) is routed through
            // PipeWire/PulseAudio to the active output (headphones, speakers, etc.).
            if (Bass.Init(Bass.DefaultDevice, DefaultOutputFrequency, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero))
            {
                _ownsBassInitialization = true;
                _initialized = true;
                var deviceInfo = Bass.GetDeviceInfo(Bass.CurrentDevice);
                _logger.LogInformation("BASS ініціалізовано (версія {Version}, пристрій {Device}: {Name})",
                    Bass.Version, Bass.CurrentDevice, deviceInfo.Name);
                return true;
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BassException)
        {
            _logger.LogError(ex, "Не вдалося завантажити або ініціалізувати BASS. Перевірте наявність libbass.so");
            SetState(PlaybackState.Error);
            return false;
        }

        var error = Bass.LastError;
        if (error == Errors.Already)
        {
            // BASS is already initialized by another component — we do not free it in Dispose.
            _ownsBassInitialization = false;
            _initialized = true;
            _logger.LogDebug("BASS уже був ініціалізований раніше");
            return true;
        }

        _logger.LogError("Не вдалося ініціалізувати BASS: {Error}", error);
        SetState(PlaybackState.Error);
        return false;
    }

    /// <summary>
    /// BASS sync handler that fires after the track has ended.
    /// </summary>
    /// <remarks>
    /// Called on the BASS thread. The stream is not released here; the release will happen
    /// during the next <see cref="PlayAsync"/>, <see cref="Stop"/>, or <see cref="Dispose"/>.
    /// Note: the first parameter is the sync handle, not the channel;
    /// the channel is passed second.
    /// </remarks>
    private void OnStreamEnded(int syncHandle, int channel, int data, IntPtr user)
    {
        var ended = false;

        lock (_sync)
        {
            if (_disposed || channel != _streamHandle)
            {
                return;
            }

            _logger.LogDebug("Відтворення завершено");
            SetState(PlaybackState.Stopped);
            ended = true;
        }

        if (ended)
        {
            RaiseTrackEnded();
        }
    }

    /// <summary>
    /// Notifies subscribers about the natural end of the track, outside the lock.
    /// </summary>
    private void RaiseTrackEnded()
    {
        try
        {
            TrackEnded?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події TrackEnded");
        }
    }

    /// <summary>
    /// Stops and releases the current stream, if one exists.
    /// </summary>
    private void StopCore()
    {
        if (_streamHandle == 0)
        {
            return;
        }

        var handle = _streamHandle;
        _streamHandle = 0;
        _wavBuffer = null;

        try
        {
            Bass.ChannelStop(handle);

            if (!Bass.StreamFree(handle))
            {
                _logger.LogWarning("Не вдалося звільнити аудіопотік: {Error}", Bass.LastError);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or BassException)
        {
            _logger.LogWarning(ex, "Помилка під час звільнення аудіопотоку");
        }
    }

    /// <summary>
    /// Releases a stream that failed to be created.
    /// </summary>
    private void ReleaseStream()
    {
        var handle = _streamHandle;
        _streamHandle = 0;
        _wavBuffer = null;

        if (handle != 0)
        {
            try
            {
                Bass.StreamFree(handle);
            }
            catch (Exception ex) when (ex is DllNotFoundException or BassException)
            {
                _logger.LogWarning(ex, "Помилка під час звільнення невдалого аудіопотоку");
            }
        }
    }

    /// <summary>
    /// Changes the state and notifies subscribers.
    /// </summary>
    private void SetState(PlaybackState state)
    {
        if (_state == state)
        {
            return;
        }

        _state = state;

        try
        {
            StateChanged?.Invoke(this, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події StateChanged");
        }
    }

    /// <summary>
    /// Releases the audio stream and, if necessary, BASS itself.
    /// </summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopCore();

            if (_ownsBassInitialization)
            {
                try
                {
                    Bass.Free();
                    _logger.LogInformation("BASS звільнено");
                }
                catch (Exception ex) when (ex is DllNotFoundException or BassException)
                {
                    _logger.LogWarning(ex, "Помилка під час звільнення BASS");
                }

                _ownsBassInitialization = false;
            }

            _initialized = false;
        }

        StateChanged = null;
        TrackEnded = null;
    }
}
