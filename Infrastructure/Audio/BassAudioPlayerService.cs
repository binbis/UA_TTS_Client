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
/// Відтворює WAV-аудіо з пам'яті через BASS (ManagedBass 4.0.2).
/// </summary>
/// <remarks>
/// <para>Масив <c>byte[]</c> з WAV-ом утримується в полі до кінця відтворення: BASS читає
/// його безпосередньо, тому збирання сміття не повинно звільнити буфер.</para>
/// <para>Звук ніколи не пишеться на диск — усе відбувається в пам'яті.</para>
/// </remarks>
public sealed class BassAudioPlayerService : IAudioPlayerService, IDisposable
{
    private const int DefaultDevice = 0;
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
    /// Створює сервіс відтворення.
    /// </summary>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException"><paramref name="logger"/> дорівнює <see langword="null"/>.</exception>
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

            // Синхронізація "кінець треку" потрібна для автопереходу в Stopped.
            if (Bass.ChannelSetSync(handle, SyncFlags.End, 0, _endSyncProcedure, IntPtr.Zero) == 0)
            {
                _logger.LogWarning("Не вдалося встановити синхронізацію завершення треку: {Error}", Bass.LastError);
            }
            Bass.ChannelSetAttribute(handle, ChannelAttribute.Volume, (float)_volume);

            // restart: true — почати з нуля, навіть якщо потік щойно створено.
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
    /// Ініціалізує BASS один раз на весь час життя сервісу.
    /// </summary>
    /// <returns><see langword="true"/>, якщо пристрій готовий до відтворення.</returns>
    private bool EnsureInitialized()
    {
        if (_initialized)
        {
            return true;
        }

        try
        {
            if (Bass.Init(DefaultDevice, DefaultOutputFrequency, DeviceInitFlags.Default, IntPtr.Zero, IntPtr.Zero))
            {
                _ownsBassInitialization = true;
                _initialized = true;
                _logger.LogInformation("BASS ініціалізовано (версія {Version}, пристрій {Device})",
                    Bass.Version, Bass.CurrentDevice);
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
            // BASS уже ініціалізований іншим компонентом — не звільняємо його у Dispose.
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
    /// Обробник синхронізації BASS, що спрацьовує після завершення треку.
    /// </summary>
    /// <remarks>
    /// Викликається у потоці BASS. Потік тут не звільняється: звільнення відбудеться
    /// під час наступного <see cref="PlayAsync"/>, <see cref="Stop"/> або <see cref="Dispose"/>.
    /// Зверніть увагу: перший параметр — це дескриптор синхронізації, а не каналу;
    /// канал передається другим.
    /// </remarks>
    private void OnStreamEnded(int syncHandle, int channel, int data, IntPtr user)
    {
        lock (_sync)
        {
            if (_disposed || channel != _streamHandle)
            {
                return;
            }

            _logger.LogDebug("Відтворення завершено");
            SetState(PlaybackState.Stopped);
        }
    }

    /// <summary>
    /// Зупиняє та звільняє поточний потік, якщо він існує.
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
    /// Звільняє невдало створений потік.
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
    /// Змінює стан і повідомляє підписників.
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
    /// Звільняє аудіопотік і (за потреби) сам BASS.
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
    }
}
