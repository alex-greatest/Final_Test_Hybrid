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
        StateManager.ClearErrors();
        _activityTracker.SetTestExecutionActive(true);
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
        _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
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
        var finalState = StateManager.State == ExecutionState.Failed || HasErrors
            ? ExecutionState.Failed
            : ExecutionState.Completed;
        StateManager.TransitionTo(finalState);
        _activityTracker.SetTestExecutionActive(false);
        LogExecutionCompleted();
        OnSequenceCompleted?.Invoke();
        lock (_stateLock)
        {
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void LogExecutionCompleted()
    {
        _logger.LogInformation("Завершено. Ошибки: {HasErrors}", HasErrors);
        var result = HasErrors ? "С ОШИБКАМИ" : "УСПЕШНО";
        _testLogger.LogInformation("═══ ТЕСТИРОВАНИЕ ЗАВЕРШЕНО: {Result} ═══", result);
    }

    public void Stop(string reason = "оператором")
    {
        CancellationTokenSource? ctsToCancel;
        lock (_stateLock)
        {
            ctsToCancel = _cts;
            if (ctsToCancel == null || ctsToCancel.IsCancellationRequested)
            {
                return;
            }
            LogStopRequested(reason);
        }
        ctsToCancel?.Cancel();
    }

    private void LogStopRequested(string reason)
    {
        _logger.LogInformation("Остановка: {Reason}", reason);
        _testLogger.LogWarning("Тестирование остановлено {Reason}", reason);
    }
}
