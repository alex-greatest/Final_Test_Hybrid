using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    public void SetMaps(List<TestMap> maps)
    {
        lock (_stateLock)
        {
            if (IsRunning)
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
        _logger.LogWarning("Попытка загрузить Maps во время выполнения");
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
        StateManager.SetHasErrors(false);
    }

    private void LogMapsLoaded(int count)
    {
        _logger.LogInformation("Загружено {Count} Maps", count);
        _testLogger.LogInformation("Загружена последовательность из {Count} блоков", count);
    }
}
