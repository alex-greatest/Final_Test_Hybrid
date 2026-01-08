namespace Final_Test_Hybrid.Services.Diagnostic.Polling;

/// <summary>
/// Координатор паузы polling для приоритета разовых запросов.
/// Thread-safe, поддерживает nested pause.
/// </summary>
public class PollingPauseCoordinator : IDisposable
{
    private const int PauseCheckIntervalMs = 10;

    private readonly SemaphoreSlim _pauseLock = new(1, 1);
    private readonly ManualResetEventSlim _pauseEvent = new(initialState: true);

    private int _activePollCount;
    private int _pauseCount;
    private int _disposed;

    /// <summary>
    /// True если polling приостановлен.
    /// </summary>
    public bool IsPaused => !_pauseEvent.IsSet;

    /// <summary>
    /// Количество активных операций polling.
    /// </summary>
    public int ActivePollCount => _activePollCount;

    #region Pause / Resume

    /// <summary>
    /// Приостанавливает polling и ждёт завершения текущих операций.
    /// Поддерживает nested pause - каждый PauseAsync требует соответствующий Resume.
    /// </summary>
    public async Task PauseAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        await _pauseLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            IncrementPauseCount();
            await WaitForActivePollingToCompleteAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _pauseLock.Release();
        }
    }

    /// <summary>
    /// Возобновляет polling.
    /// Должен вызываться столько же раз, сколько был вызван PauseAsync.
    /// </summary>
    public void Resume()
    {
        ThrowIfDisposed();
        _pauseLock.Wait();
        try
        {
            DecrementPauseCount();
        }
        finally
        {
            _pauseLock.Release();
        }
    }

    private void IncrementPauseCount()
    {
        _pauseCount++;

        if (IsFirstPause())
        {
            _pauseEvent.Reset();
        }
    }

    private bool IsFirstPause()
    {
        return _pauseCount == 1;
    }

    private async Task WaitForActivePollingToCompleteAsync(CancellationToken ct)
    {
        while (HasActivePolling() && !ct.IsCancellationRequested)
        {
            await Task.Delay(PauseCheckIntervalMs, ct).ConfigureAwait(false);
        }
    }

    private bool HasActivePolling()
    {
        return _activePollCount > 0;
    }

    private void DecrementPauseCount()
    {
        if (_pauseCount <= 0)
        {
            return;
        }

        _pauseCount--;

        if (IsLastResume())
        {
            _pauseEvent.Set();
        }
    }

    private bool IsLastResume()
    {
        return _pauseCount == 0;
    }

    #endregion

    #region Poll Tracking

    /// <summary>
    /// Асинхронно ждёт если polling на паузе (для PollingTask).
    /// </summary>
    public async Task WaitIfPausedAsync(CancellationToken ct)
    {
        ThrowIfDisposed();

        while (IsPaused && !ct.IsCancellationRequested)
        {
            await Task.Delay(PauseCheckIntervalMs, ct).ConfigureAwait(false);
            ThrowIfDisposed();
        }

        ct.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// Отмечает начало операции polling.
    /// </summary>
    public void EnterPoll()
    {
        Interlocked.Increment(ref _activePollCount);
    }

    /// <summary>
    /// Отмечает конец операции polling.
    /// </summary>
    public void ExitPoll()
    {
        Interlocked.Decrement(ref _activePollCount);
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (IsAlreadyDisposed())
        {
            return;
        }

        _pauseEvent.Dispose();
        _pauseLock.Dispose();
    }

    private bool IsAlreadyDisposed()
    {
        return Interlocked.Exchange(ref _disposed, 1) == 1;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed == 1)
        {
            throw new ObjectDisposedException(nameof(PollingPauseCoordinator));
        }
    }

    #endregion
}