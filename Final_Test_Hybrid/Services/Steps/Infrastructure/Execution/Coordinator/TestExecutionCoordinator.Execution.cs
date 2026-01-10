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
        var token = GetCancellationToken();
        var maps = _maps;
        var totalMaps = maps.Count;
        for (CurrentMapIndex = 0; CurrentMapIndex < totalMaps && !ShouldStop; CurrentMapIndex++)
        {
            await RunCurrentMap(maps[CurrentMapIndex], totalMaps, token);
        }
    }

    private async Task RunCurrentMap(TestMap map, int totalMaps, CancellationToken token)
    {
        LogMapStart(totalMaps);
        await ExecuteMapOnAllColumns(map, token);
    }

    private void LogMapStart(int totalMaps)
    {
        _logger.LogInformation("Map {Index}/{Total}", CurrentMapIndex + 1, totalMaps);
        _testLogger.LogInformation("─── Блок {Index} из {Total} ───", CurrentMapIndex + 1, totalMaps);
    }

    private CancellationToken GetCancellationToken()
    {
        lock (_stateLock)
        {
            return _cts?.Token ?? CancellationToken.None;
        }
    }

    private async Task ExecuteMapOnAllColumns(TestMap map, CancellationToken token)
    {
        var executionTasks = _executors.Select(executor => executor.ExecuteMapAsync(map, token));
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

    private void LogStopRequested(string reason)
    {
        _logger.LogInformation("Остановка: {Reason}", reason);
        _testLogger.LogWarning("Тестирование остановлено {Reason}", reason);
    }
}
