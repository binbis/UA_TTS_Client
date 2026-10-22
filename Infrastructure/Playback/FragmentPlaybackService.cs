#nullable enable

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UA_TTS_Client.Core.Enums;
using UA_TTS_Client.Core.Interfaces;
using UA_TTS_Client.Core.Models;

namespace UA_TTS_Client.Infrastructure.Playback;

/// <summary>
/// Синтезує фрагменти послідовно та відтворює їх пакетами по N.
/// </summary>
/// <remarks>
/// <para>Поки поточний фрагмент грає, наступний уже синтезується (один крок попереду),
/// щоб між фрагментами не було чутної паузи.</para>
/// <para>Після пакета черга зупиняється; наступний пакет запускає <see cref="ContinueAsync"/>.</para>
/// <para>Синтез-запити до сервера послідовні: паралельні запити перевантажували б GPU.</para>
/// </remarks>
public sealed class FragmentPlaybackService : IFragmentPlaybackService, IDisposable
{
    private readonly ITtsApiClient _apiClient;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly ILogger<FragmentPlaybackService> _logger;
    private readonly object _sync = new();

    private IReadOnlyList<TextFragment> _fragments = [];
    private SpeechQueueOptions _options = new();
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<bool>? _trackEndedTcs;
    private TaskCompletionSource<bool>? _resumeTcs;
    private volatile bool _isPaused;
    private volatile bool _hasQueue;
    private volatile bool _isBatchPending;
    private volatile bool _disposed;
    private volatile int _currentFragment;
    private volatile int _totalFragments;
    private volatile int _currentBatch;
    private volatile int _totalBatches;
    private volatile int _nextFragmentIndex;
    private int _generation;

    /// <summary>
    /// Створює сервіс черги фрагментів.
    /// </summary>
    /// <param name="apiClient">Клієнт TTS-сервера.</param>
    /// <param name="audioPlayer">Сервіс відтворення аудіо.</param>
    /// <param name="logger">Логер.</param>
    /// <exception cref="ArgumentNullException">Один із аргументів дорівнює <see langword="null"/>.</exception>
    public FragmentPlaybackService(
        ITtsApiClient apiClient,
        IAudioPlayerService audioPlayer,
        ILogger<FragmentPlaybackService> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _audioPlayer = audioPlayer ?? throw new ArgumentNullException(nameof(audioPlayer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _audioPlayer.TrackEnded += OnTrackEnded;
    }

    /// <inheritdoc />
    public event EventHandler<FragmentStartedEventArgs>? FragmentStarted;

    /// <inheritdoc />
    public event EventHandler<BatchCompletedEventArgs>? BatchCompleted;

    /// <inheritdoc />
    public event EventHandler? QueueCompleted;

    /// <inheritdoc />
    public event EventHandler<Exception>? Failed;

    /// <inheritdoc />
    public bool HasQueue => _hasQueue;

    /// <inheritdoc />
    public bool IsBatchPending => _isBatchPending;

    /// <inheritdoc />
    public int CurrentFragment => _currentFragment;

    /// <inheritdoc />
    public int TotalFragments => _totalFragments;

    /// <inheritdoc />
    public int CurrentBatch => _currentBatch;

    /// <inheritdoc />
    public int TotalBatches => _totalBatches;

    /// <inheritdoc />
    public async Task StartAsync(
        IReadOnlyList<TextFragment> fragments,
        SpeechQueueOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fragments);
        ArgumentNullException.ThrowIfNull(options);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (fragments.Count == 0)
        {
            throw new ArgumentException("Список фрагментів порожній.", nameof(fragments));
        }

        var batchSize = Math.Clamp(options.BatchSize, SpeechQueueOptions.MinBatchSize, SpeechQueueOptions.MaxBatchSize);
        var totalBatches = (int)Math.Ceiling(fragments.Count / (double)batchSize);

        CancellationTokenSource cts;
        lock (_sync)
        {
            StopCore(resetCounters: true);

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cts = cts;
            _fragments = fragments;
            _options = options with { BatchSize = batchSize };
            _nextFragmentIndex = 0;
            _currentFragment = 0;
            _totalFragments = fragments.Count;
            _currentBatch = 1;
            _totalBatches = totalBatches;
            _hasQueue = true;
            _isBatchPending = false;
        }

        _logger.LogDebug("Черга фрагментів: {Count} фрагментів, пакетами по {BatchSize}",
            fragments.Count, batchSize);

        await StartBatchAsync(0, cts.Token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ContinueAsync(CancellationToken cancellationToken = default)
    {
        CancellationToken token;

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!_isBatchPending || _cts is null)
            {
                throw new InvalidOperationException("Немає завершеного пакета, який можна продовжити.");
            }

            token = _cts.Token;
            _isBatchPending = false;
        }

        await StartBatchAsync(_nextFragmentIndex, token).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Pause()
    {
        lock (_sync)
        {
            if (_disposed || !_hasQueue || _isPaused)
            {
                return;
            }

            _isPaused = true;
        }

        _audioPlayer.Pause();
    }

    /// <inheritdoc />
    public void Resume()
    {
        TaskCompletionSource<bool>? resumeSignal;

        lock (_sync)
        {
            if (!_isPaused)
            {
                return;
            }

            _isPaused = false;
            resumeSignal = _resumeTcs;
            _resumeTcs = null;
        }

        _audioPlayer.Resume();
        resumeSignal?.TrySetResult(true);
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

            StopCore(resetCounters: true);
        }
    }

    /// <summary>
    /// Запускає фоновий pipeline поточного пакета й чекає на старт першого фрагмента.
    /// </summary>
    private async Task StartBatchAsync(int startIndex, CancellationToken cancellationToken)
    {
        var generation = _generation;
        var firstStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Pipeline не очікується тут: StartAsync/ContinueAsync повертаються після старту
        // першого фрагмента, а решта пакета грає у фоні.
        _ = RunQueueAsync(generation, startIndex, firstStarted, cancellationToken);

        await firstStarted.Task.ConfigureAwait(false);
    }

    /// <summary>Виконує один пакет і обробляє його завершення.</summary>
    private async Task RunQueueAsync(
        int generation,
        int startIndex,
        TaskCompletionSource<bool> firstStarted,
        CancellationToken cancellationToken)
    {
        try
        {
            await RunBatchAsync(generation, startIndex, firstStarted, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            firstStarted.TrySetCanceled(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Черга фрагментів зупинилася з помилкою");
            firstStarted.TrySetException(ex);

            if (generation == _generation)
            {
                lock (_sync)
                {
                    _hasQueue = false;
                    _isBatchPending = false;
                }

                RaiseFailed(ex);
            }
        }
    }

    /// <summary>Синтезує й відтворює фрагменти одного пакета.</summary>
    private async Task RunBatchAsync(
        int generation,
        int startIndex,
        TaskCompletionSource<bool> firstStarted,
        CancellationToken cancellationToken)
    {
        var fragments = _fragments;
        var options = _options;
        var batchSize = options.BatchSize;
        var end = Math.Min(startIndex + batchSize, fragments.Count);

        Task<byte[]>? nextWavTask = SynthesizeAsync(fragments[startIndex], options, cancellationToken);

        for (var index = startIndex; index < end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (generation != _generation)
            {
                return;
            }

            var wav = await nextWavTask!.ConfigureAwait(false);
            nextWavTask = index + 1 < end
                ? SynthesizeAsync(fragments[index + 1], options, cancellationToken)
                : null;

            await WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);

            if (generation != _generation)
            {
                return;
            }

            int batchNumber;
            lock (_sync)
            {
                _currentFragment = index + 1;
                _currentBatch = (index / batchSize) + 1;
                batchNumber = _currentBatch;
            }

            await PlayAndWaitAsync(wav, fragments[index], batchNumber, firstStarted, cancellationToken)
                .ConfigureAwait(false);
        }

        if (generation != _generation)
        {
            return;
        }

        var nextIndex = end;
        lock (_sync)
        {
            _nextFragmentIndex = nextIndex;
        }

        if (nextIndex < fragments.Count)
        {
            int completedBatch;
            lock (_sync)
            {
                _isBatchPending = true;
                completedBatch = _currentBatch;
            }

            RaiseBatchCompleted(completedBatch, _totalBatches, hasNext: true);
        }
        else
        {
            lock (_sync)
            {
                _hasQueue = false;
                _isBatchPending = false;
            }

            RaiseQueueCompleted();
        }
    }

    /// <summary>Синтезує один фрагмент через сервер.</summary>
    private async Task<byte[]> SynthesizeAsync(
        TextFragment fragment,
        SpeechQueueOptions options,
        CancellationToken cancellationToken)
    {
        var request = new TtsRequestDto
        {
            Text = fragment.Text,
            Voice = options.Voice,
            Speed = options.Speed,
            Volume = 1.0,
            Translate = false
        };

        return await _apiClient.SynthesizeAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Запускає відтворення фрагмента й чекає на природний кінець треку.</summary>
    private async Task PlayAndWaitAsync(
        byte[] wav,
        TextFragment fragment,
        int batchNumber,
        TaskCompletionSource<bool> firstStarted,
        CancellationToken cancellationToken)
    {
        var trackEnded = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        lock (_sync)
        {
            _trackEndedTcs = trackEnded;
        }

        try
        {
            await _audioPlayer.PlayAsync(wav, cancellationToken).ConfigureAwait(false);

            if (_audioPlayer.State == PlaybackState.Error)
            {
                throw new InvalidOperationException("Не вдалося відтворити аудіофрагмент.");
            }

            RaiseFragmentStarted(fragment, batchNumber, _totalBatches);
            firstStarted.TrySetResult(true);

            // Пауза могла надійти, доки фрагмент ще запускався: застосовуємо її після старту.
            if (_isPaused)
            {
                _audioPlayer.Pause();
            }

            await trackEnded.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            lock (_sync)
            {
                if (ReferenceEquals(_trackEndedTcs, trackEnded))
                {
                    _trackEndedTcs = null;
                }
            }
        }
    }

    /// <summary>Чекає, доки черга не вийде з паузи, якщо вона на паузі.</summary>
    private async Task WaitIfPausedAsync(CancellationToken cancellationToken)
    {
        Task waitTask;

        lock (_sync)
        {
            if (!_isPaused)
            {
                return;
            }

            _resumeTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            waitTask = _resumeTcs.Task;
        }

        await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Обробник природного завершення треку BASS.</summary>
    private void OnTrackEnded(object? sender, EventArgs e)
    {
        TaskCompletionSource<bool>? trackEnded;

        lock (_sync)
        {
            trackEnded = _trackEndedTcs;
            _trackEndedTcs = null;
        }

        trackEnded?.TrySetResult(true);
    }

    /// <summary>Скасовує поточну чергу; викликається під <see cref="_sync"/>.</summary>
    private void StopCore(bool resetCounters)
    {
        _generation++;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isPaused = false;
        _hasQueue = false;
        _isBatchPending = false;

        _resumeTcs?.TrySetResult(true);
        _resumeTcs = null;
        _trackEndedTcs = null;

        _audioPlayer.Stop();

        if (resetCounters)
        {
            _currentFragment = 0;
            _totalFragments = 0;
            _currentBatch = 0;
            _totalBatches = 0;
            _nextFragmentIndex = 0;
        }
    }

    /// <summary>Повідомляє про старт фрагмента, не даючи обробнику зламати чергу.</summary>
    private void RaiseFragmentStarted(TextFragment fragment, int batch, int totalBatches)
    {
        try
        {
            FragmentStarted?.Invoke(this, new FragmentStartedEventArgs(fragment, batch, totalBatches));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події FragmentStarted");
        }
    }

    /// <summary>Повідомляє про завершення пакета.</summary>
    private void RaiseBatchCompleted(int completedBatch, int totalBatches, bool hasNext)
    {
        try
        {
            BatchCompleted?.Invoke(this, new BatchCompletedEventArgs(completedBatch, totalBatches, hasNext));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події BatchCompleted");
        }
    }

    /// <summary>Повідомляє про завершення всієї черги.</summary>
    private void RaiseQueueCompleted()
    {
        try
        {
            QueueCompleted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події QueueCompleted");
        }
    }

    /// <summary>Повідомляє про помилку черги після старту.</summary>
    private void RaiseFailed(Exception exception)
    {
        try
        {
            Failed?.Invoke(this, exception);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Помилка в обробнику події Failed");
        }
    }

    /// <summary>Зупиняє чергу й відписується від подій аудіосервісу.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopCore(resetCounters: true);
        }

        _audioPlayer.TrackEnded -= OnTrackEnded;
    }
}
