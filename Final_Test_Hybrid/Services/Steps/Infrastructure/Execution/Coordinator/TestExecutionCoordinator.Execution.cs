using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    /// <summary>
    /// Запускает выполнение тестов с ожиданием завершения.
    /// </summary>
    public async Task StartAsync()
    {
        if (!TryStart())
        {
            return;
        }
        await RunWithErrorHandlingAsync();
    }

    /// <summary>
    /// Запускает выполнение тестов в фоновом режиме.
    /// </summary>
    public bool TryStartInBackground()
    {
        if (!TryStart())
        {
            return false;
        }
        _ = RunWithErrorHandlingAsync();
        return true;
    }

    /// <summary>
    /// Логирует необработанное исключение.
    /// </summary>
    private void LogUnhandledException(Exception ex)
    {
        _logger.LogError(ex, "Необработанная ошибка в TestExecutionCoordinator");
        _testLogger.LogError(ex, "Критическая ошибка выполнения тестов");
    }

    /// <summary>
    /// Пытается начать выполнение тестов.
    /// </summary>
    private bool TryStart()
    {
        lock (_stateLock)
        {
            if (!CanStart())
            {
                return false;
            }
            BeginExecution();
            return true;
        }
    }

    /// <summary>
    /// Выполняет тесты с обработкой ошибок.
    /// </summary>
    private async Task RunWithErrorHandlingAsync()
    {
        try
        {
            await RunAllMaps();
        }
        catch (Exception ex)
        {
            LogUnhandledException(ex);
        }
        finally
        {
            Complete();
        }
    }

    /// <summary>
    /// Проверяет возможность запуска.
    /// </summary>
    private bool CanStart()
    {
        if (!StateManager.IsActive)
        {
            return ValidateMapsLoaded();
        }
        _logger.LogWarning("Координатор уже выполняется");
        return false;
    }

    /// <summary>
    /// Проверяет наличие загруженных карт.
    /// </summary>
    private bool ValidateMapsLoaded()
    {
        if (HasMapsLoaded())
        {
            return true;
        }
        LogNoSequenceLoaded();
        return false;
    }

    /// <summary>
    /// Возвращает true, если карты загружены.
    /// </summary>
    private bool HasMapsLoaded()
    {
        return _maps.Count > 0;
    }

    /// <summary>
    /// Логирует отсутствие загруженной последовательности.
    /// </summary>
    private void LogNoSequenceLoaded()
    {
        _logger.LogError("Последовательность не загружена");
        _testLogger.LogError(null, "Ошибка: последовательность тестов не загружена");
    }

    /// <summary>
    /// Инициализирует состояние для начала выполнения.
    /// </summary>
    private void BeginExecution()
    {
        _flowState.ClearStop();
        StateManager.TransitionTo(ExecutionState.Running);
        StateManager.ClearErrors();
        StateManager.ResetErrorTracking();
        _activityTracker.SetTestExecutionActive(true);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
        _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
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
    /// Выполняет карту на всех колонках с параллельной обработкой ошибок.
    /// </summary>
    private Task ExecuteMapOnAllColumns(TestMap map, int mapIndex, Guid mapRunId, CancellationToken token)
    {
        var errorChannel = StartErrorSignalChannel();
        var errorLoopTask = RunErrorHandlingLoopAsync(errorChannel.Reader, token);
        var executionTask = RunExecutorsAsync(map, mapIndex, mapRunId, token);
        var completionTask = executionTask.ContinueWith(
            _ => CompleteErrorSignalChannel(),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return Task.WhenAll(executionTask, errorLoopTask, completionTask);
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
        if (AreExecutorsIdle())
        {
            return;
        }
        await Task.Delay(50, ct);
        await WaitForExecutorsIdleAsync(ct);
    }

    private bool AreExecutorsIdle()
    {
        return _executors.All(executor => !executor.IsVisible);
    }

    private (int MapIndex, Guid RunId) GetActiveMapSnapshot()
    {
        lock (_stateLock)
        {
            return (_activeMapIndex, _activeMapRunId);
        }
    }

    /// <summary>
    /// Завершает выполнение и освобождает ресурсы.
    /// </summary>
    private void Complete()
    {
        var flowSnapshot = _flowState.GetSnapshot();
        var isSuccessful = !(StateManager.State == ExecutionState.Failed || HasErrors || flowSnapshot.StopAsFailure);
        var finalState = isSuccessful ? ExecutionState.Completed : ExecutionState.Failed;
        StateManager.TransitionTo(finalState);
        _activityTracker.SetTestExecutionActive(false);
        _errorService.ClearActiveApplicationErrors();
        LogExecutionCompleted(isSuccessful, flowSnapshot);
        InvokeSequenceCompletedSafely();
        lock (_stateLock)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <summary>
    /// Безопасно вызывает OnSequenceCompleted с перехватом исключений из обработчиков.
    /// </summary>
    private void InvokeSequenceCompletedSafely()
    {
        try
        {
            OnSequenceCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в обработчике OnSequenceCompleted");
        }
    }

    /// <summary>
    /// Логирует завершение выполнения.
    /// </summary>
    private void LogExecutionCompleted(bool isSuccessful, (ExecutionStopReason Reason, bool StopAsFailure) flowSnapshot)
    {
        _logger.LogInformation(
            "Завершено. Ошибки: {HasErrors}, StopAsFailure: {StopAsFailure}, StopReason: {StopReason}",
            HasErrors,
            flowSnapshot.StopAsFailure,
            flowSnapshot.Reason);
        var result = isSuccessful ? "УСПЕШНО" : "С ОШИБКАМИ";
        _testLogger.LogInformation("═══ ТЕСТИРОВАНИЕ ЗАВЕРШЕНО: {Result} ═══", result);
    }

    /// <summary>
    /// Останавливает выполнение тестов.
    /// </summary>
    public void Stop(string reason = "оператором", bool markFailed = false)
    {
        Stop(ExecutionStopReason.Operator, reason, markFailed);
    }

    /// <summary>
    /// Останавливает выполнение тестов с указанной причиной.
    /// </summary>
    public void Stop(ExecutionStopReason stopReason, string reason, bool markFailed = false)
    {
        _flowState.RequestStop(stopReason, markFailed);
        CancelExecution(reason);
    }

    /// <summary>
    /// Отменяет выполнение.
    /// </summary>
    private void CancelExecution(string reason)
    {
        lock (_stateLock)
        {
            if (_cts == null || _cts.IsCancellationRequested)
            {
                return;
            }
            LogStopRequested(reason);
            _cts.Cancel();
        }
    }

    /// <summary>
    /// Логирует запрос на остановку.
    /// </summary>
    private void LogStopRequested(string reason)
    {
        _logger.LogInformation("Остановка: {Reason}", reason);
        _testLogger.LogWarning("Тестирование остановлено {Reason}", reason);
    }
}
