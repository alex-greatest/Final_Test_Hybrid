using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    public async Task StartAsync()
    {
        if (!TryStart())
        {
            return;
        }
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

    private void LogUnhandledException(Exception ex)
    {
        _logger.LogError(ex, "Необработанная ошибка в TestExecutionCoordinator");
        _testLogger.LogError(ex, "Критическая ошибка выполнения тестов");
    }

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

    private bool CanStart()
    {
        if (!StateManager.IsActive)
        {
            return ValidateMapsLoaded();
        }
        _logger.LogWarning("Координатор уже выполняется");
        return false;
    }

    private bool ValidateMapsLoaded()
    {
        if (HasMapsLoaded())
        {
            return true;
        }
        LogNoSequenceLoaded();
        return false;
    }

    private bool HasMapsLoaded()
    {
        return _maps.Count > 0;
    }

    private void LogNoSequenceLoaded()
    {
        _logger.LogError("Последовательность не загружена");
        _testLogger.LogError(null, "Ошибка: последовательность тестов не загружена");
    }

    private void BeginExecution()
    {
        StateManager.TransitionTo(ExecutionState.Running);
        _activityTracker.SetTestExecutionActive(true);
        CancelPendingErrorResolution();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
        _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
    }

    private void CancelPendingErrorResolution()
    {
        lock (_stateLock)
        {
            _errorResolutionTcs?.TrySetCanceled();
            _errorResolutionTcs = null;
        }
    }

    private async Task RunAllMaps()
    {
        var maps = _maps;
        var totalMaps = maps.Count;
        for (CurrentMapIndex = 0; CurrentMapIndex < totalMaps && !ShouldStop; CurrentMapIndex++)
        {
            await RunCurrentMap(maps[CurrentMapIndex], totalMaps);
        }
    }

    private async Task RunCurrentMap(TestMap map, int totalMaps)
    {
        LogMapStart(totalMaps);
        await ExecuteMapOnAllColumns(map);
    }

    private void LogMapStart(int totalMaps)
    {
        _logger.LogInformation("Map {Index}/{Total}", CurrentMapIndex + 1, totalMaps);
        _testLogger.LogInformation("─── Блок {Index} из {Total} ───", CurrentMapIndex + 1, totalMaps);
    }

    private async Task ExecuteMapOnAllColumns(TestMap map)
    {
        var executionTasks = _executors.Select(executor => executor.ExecuteMapAsync(map, _cts!.Token));
        await Task.WhenAll(executionTasks);
        await HandleErrorsIfAny();
    }

    private void Complete()
    {
        var finalState = HasErrors ? ExecutionState.Failed : ExecutionState.Completed;
        StateManager.TransitionTo(finalState);
        _activityTracker.SetTestExecutionActive(false);
        LogExecutionCompleted();
        OnSequenceCompleted?.Invoke();
    }

    private void LogExecutionCompleted()
    {
        _logger.LogInformation("Завершено. Ошибки: {HasErrors}", HasErrors);
        var result = HasErrors ? "С ОШИБКАМИ" : "УСПЕШНО";
        _testLogger.LogInformation("═══ ТЕСТИРОВАНИЕ ЗАВЕРШЕНО: {Result} ═══", result);
    }

    public void Stop()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_stateLock)
        {
            if (!IsRunning)
            {
                return;
            }
            LogStopRequested();
            ctsToCancel = _cts;
        }
        ctsToCancel?.Cancel();
    }

    private void LogStopRequested()
    {
        _logger.LogInformation("Остановка");
        _testLogger.LogWarning("Тестирование остановлено оператором");
    }
}
