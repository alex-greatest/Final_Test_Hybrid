using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Polling;

/// <summary>
/// Задача периодического опроса регистров ЭБУ котла.
/// </summary>
public class PollingTask(
    string name,
    ushort[] addresses,
    TimeSpan interval,
    Func<Dictionary<ushort, object>, Task> callback,
    RegisterReader reader,
    PollingPauseCoordinator pauseCoordinator,
    ILogger<PollingTask> logger,
    ITestStepLogger testStepLogger)
    : IAsyncDisposable
{
    private readonly DualLogger<PollingTask> _logger = new(logger, testStepLogger);
    private readonly object _startLock = new();

    private CancellationTokenSource? _cts;
    private Task? _pollingTask;
    private bool _disposed;

    /// <summary>
    /// Имя задачи опроса.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Адреса регистров для опроса.
    /// </summary>
    public ushort[] Addresses { get; } = [..addresses];

    /// <summary>
    /// Интервал опроса.
    /// </summary>
    public TimeSpan Interval { get; } = interval;

    /// <summary>
    /// Флаг активности задачи.
    /// </summary>
    public bool IsRunning
    {
        get
        {
            lock (_startLock)
            {
                return IsRunningUnsafe;
            }
        }
    }

    private bool IsRunningUnsafe => _pollingTask is { IsCompleted: false };

    #region Start / Stop

    /// <summary>
    /// Запускает задачу опроса.
    /// </summary>
    public void Start()
    {
        lock (_startLock)
        {
            if (IsRunningUnsafe)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            _pollingTask = RunPollingLoopAsync(_cts.Token);
        }

        _logger.LogDebug("Задача опроса '{Name}' запущена", Name);
    }

    /// <summary>
    /// Останавливает задачу опроса.
    /// </summary>
    public async Task StopAsync()
    {
        Task? taskToWait;
        CancellationTokenSource? ctsToCancel;

        lock (_startLock)
        {
            if (!IsRunningUnsafe)
            {
                return;
            }

            taskToWait = _pollingTask;
            ctsToCancel = _cts;
        }

        await CancelPollingAsync(ctsToCancel).ConfigureAwait(false);
        await WaitForPollingToCompleteAsync(taskToWait).ConfigureAwait(false);

        CleanupPollingResources();

        _logger.LogDebug("Задача опроса '{Name}' остановлена", Name);
    }

    private static async Task CancelPollingAsync(CancellationTokenSource? cts)
    {
        if (cts != null)
        {
            await cts.CancelAsync();
        }
    }

    private static async Task WaitForPollingToCompleteAsync(Task? pollingTask)
    {
        if (pollingTask == null)
        {
            return;
        }

        try
        {
            await pollingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected exception on cancellation
        }
    }

    private void CleanupPollingResources()
    {
        lock (_startLock)
        {
            _cts?.Dispose();
            _cts = null;
            _pollingTask = null;
        }
    }

    #endregion

    #region Polling Loop

    private async Task RunPollingLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            await ExecuteSinglePollIterationAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task ExecuteSinglePollIterationAsync(CancellationToken ct)
    {
        await pauseCoordinator.WaitIfPausedAsync(ct).ConfigureAwait(false);

        pauseCoordinator.EnterPoll();
        try
        {
            await ExecutePollWithErrorHandlingAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            pauseCoordinator.ExitPoll();
        }
    }

    private async Task ExecutePollWithErrorHandlingAsync(CancellationToken ct)
    {
        try
        {
            await PollRegistersAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в задаче опроса '{Name}': {Error}", Name, ex.Message);
        }
    }

    private async Task PollRegistersAsync(CancellationToken ct)
    {
        var results = await ReadAllRegistersAsync(ct).ConfigureAwait(false);

        await NotifyIfHasResultsAsync(results).ConfigureAwait(false);
    }

    private async Task<Dictionary<ushort, object>> ReadAllRegistersAsync(CancellationToken ct)
    {
        var results = new Dictionary<ushort, object>();

        foreach (var address in Addresses)
        {
            await ReadSingleRegisterAsync(address, results, ct).ConfigureAwait(false);
        }

        return results;
    }

    private async Task ReadSingleRegisterAsync(
        ushort address,
        Dictionary<ushort, object> results,
        CancellationToken ct)
    {
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            results[address] = result.Value!;
        }
    }

    private async Task NotifyIfHasResultsAsync(Dictionary<ushort, object> results)
    {
        if (results.Count > 0)
        {
            await callback(results).ConfigureAwait(false);
        }
    }

    #endregion

    #region Dispose

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await StopAsync().ConfigureAwait(false);
    }

    #endregion
}