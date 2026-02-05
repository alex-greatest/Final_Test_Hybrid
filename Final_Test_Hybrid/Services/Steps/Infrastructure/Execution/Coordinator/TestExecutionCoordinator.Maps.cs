using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    public void SetMaps(List<TestMap> maps)
    {
        lock (_stateLock)
        {
            if (StateManager.IsActive)
            {
                LogCannotLoadMapsWhileRunning();
                return;
            }
            ApplyNewMaps(maps);
            ResetAllExecutors();
            LogMapsLoaded(maps.Count);
        }
    }

    private void LogCannotLoadMapsWhileRunning()
    {
        _logger.LogWarning("Попытка загрузить карты во время выполнения");
    }

    private void ApplyNewMaps(List<TestMap> maps)
    {
        _maps = [..maps];
        CurrentMapIndex = 0;
    }

    private void ResetAllExecutors()
    {
        foreach (var executor in _executors)
        {
            executor.Reset();
        }
        StateManager.ClearErrors();
    }

    private void LogMapsLoaded(int count)
    {
        _logger.LogInformation("Загружено {Count} карт", count);
        _testLogger.LogInformation("Загружена последовательность из {Count} блоков", count);
    }

    /// <summary>
    /// Выполняет все карты последовательно.
    /// </summary>
    private async Task RunAllMaps()
    {
        var token = GetCancellationToken();
        var maps = _maps;
        var totalMaps = maps.Count;
        for (CurrentMapIndex = 0; CurrentMapIndex < totalMaps && !ShouldStop; CurrentMapIndex++)
        {
            await RunCurrentMap(maps[CurrentMapIndex], totalMaps, token);
        }
    }

    /// <summary>
    /// Выполняет текущую карту.
    /// </summary>
    private async Task RunCurrentMap(TestMap map, int totalMaps, CancellationToken token)
    {
        await WaitForExecutorsIdleAsync(token);
        LogMapStart(totalMaps);
        var runId = ActivateMap(CurrentMapIndex);
        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            await ExecuteMapOnAllColumns(map, CurrentMapIndex, runId, token);
            await WaitForMapSettlementAsync(token);
        }
        finally
        {
            DeactivateMap(CurrentMapIndex, runId, startTimestamp);
        }
    }

    /// <summary>
    /// Логирует начало выполнения карты.
    /// </summary>
    private void LogMapStart(int totalMaps)
    {
        _logger.LogInformation("Map {Index}/{Total}", CurrentMapIndex + 1, totalMaps);
        _testLogger.LogInformation("─── Блок {Index} из {Total} ───", CurrentMapIndex + 1, totalMaps);
    }

    /// <summary>
    /// Возвращает токен отмены.
    /// </summary>
    private CancellationToken GetCancellationToken()
    {
        lock (_stateLock)
        {
            return _cts?.Token ?? CancellationToken.None;
        }
    }

    /// <summary>
    /// Выполняет карту на всех колонках с обработкой ошибок через event loop.
    /// </summary>
    private Task ExecuteMapOnAllColumns(TestMap map, int mapIndex, Guid mapRunId, CancellationToken token)
    {
        return RunExecutorsAsync(map, mapIndex, mapRunId, token);
    }

    /// <summary>
    /// Запускает выполнение карты на всех колонках.
    /// </summary>
    private Task RunExecutorsAsync(TestMap map, int mapIndex, Guid mapRunId, CancellationToken token)
    {
        var executionTasks = _executors.Select(executor => executor.ExecuteMapAsync(map, mapIndex, mapRunId, token));
        return Task.WhenAll(executionTasks);
    }

    private Guid ActivateMap(int mapIndex)
    {
        var runId = Guid.NewGuid();
        lock (_stateLock)
        {
            _activeMapIndex = mapIndex;
            _activeMapRunId = runId;
            _mapGate.Set();
        }
        _logger.LogDebug("Map gate opened: {MapIndex}, RunId={RunId}", mapIndex, runId);
        return runId;
    }

    private void DeactivateMap(int mapIndex, Guid runId, long startTimestamp)
    {
        lock (_stateLock)
        {
            _activeMapIndex = -1;
            _activeMapRunId = Guid.Empty;
            _mapGate.Reset();
        }
        var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
        _logger.LogDebug(
            "Map gate closed: {MapIndex}, RunId={RunId}, Duration={DurationMs}ms",
            mapIndex,
            runId,
            elapsed.TotalMilliseconds);
    }

    private async Task WaitForExecutorsIdleAsync(CancellationToken ct)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        while (!AreExecutorsIdle())
        {
            await Task.Delay(ExecutorsIdlePollInterval, ct);
            startTimestamp = ShouldFreezeIdleTimeout()
                ? Stopwatch.GetTimestamp()
                : startTimestamp;
            _ = !ct.IsCancellationRequested && Stopwatch.GetElapsedTime(startTimestamp) > ExecutorsIdleTimeout
                ? ThrowExecutorsIdleTimeout()
                : 0;
        }
    }

    private async Task WaitForMapSettlementAsync(CancellationToken ct)
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        while (!IsMapSettled())
        {
            await Task.Delay(SettlementPollInterval, ct);
            startTimestamp = ShouldFreezeIdleTimeout()
                ? Stopwatch.GetTimestamp()
                : startTimestamp;
            _ = !ct.IsCancellationRequested && Stopwatch.GetElapsedTime(startTimestamp) > SettlementTimeout
                ? ThrowSettlementTimeout()
                : 0;
        }
    }

    private bool AreExecutorsIdle()
    {
        return !StateManager.HasPendingErrors
            && !_retryState.IsActive
            && !HasPendingRetries()
            && _executors.All(executor => !executor.HasFailed);
    }

    private bool IsMapSettled()
    {
        return !StateManager.HasPendingErrors
            && (_errorDrainTask == null || _errorDrainTask.IsCompleted)
            && !_retryState.IsActive
            && !HasPendingRetries();
    }

    private (int MapIndex, Guid RunId) GetActiveMapSnapshot()
    {
        lock (_stateLock)
        {
            return (_activeMapIndex, _activeMapRunId);
        }
    }
}
