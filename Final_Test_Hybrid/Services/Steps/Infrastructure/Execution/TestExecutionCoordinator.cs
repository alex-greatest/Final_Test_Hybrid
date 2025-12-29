using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestExecutionCoordinator : IDisposable
{
    private const int ColumnCount = 4;
    private readonly ColumnExecutor[] _executors;
    private readonly ILogger<TestExecutionCoordinator> _logger;
    private readonly ITestStepLogger _testLogger;
    private readonly Lock _stateLock = new();
    private readonly Action _onExecutorStateChanged;
    private List<TestMap> _maps = [];
    private CancellationTokenSource? _cts;
    public event Action? OnStateChanged;
    public event Action? OnSequenceCompleted;
    public IReadOnlyList<ColumnExecutor> Executors => _executors;
    public int CurrentMapIndex { get; private set; }
    public int TotalMaps => _maps.Count;
    public bool IsRunning { get; private set; }
    public bool HasErrors => _executors.Any(e => e.HasFailed);

    private bool ShouldStop => IsCancellationRequested || HasErrors;
    private bool IsCancellationRequested => _cts?.IsCancellationRequested == true;

    public TestExecutionCoordinator(
        OpcUaTagService opcUaTagService,
        ILogger<TestExecutionCoordinator> logger,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        TestSequenseService sequenseService)
    {
        _logger = logger;
        _testLogger = testLogger;
        _onExecutorStateChanged = () => OnStateChanged?.Invoke();
        _executors = CreateAllExecutors(opcUaTagService, testLogger, loggerFactory, sequenseService);
        SubscribeToExecutorEvents();
    }

    private ColumnExecutor[] CreateAllExecutors(
        OpcUaTagService opcUaTagService,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        TestSequenseService sequenseService)
    {
        return Enumerable.Range(0, ColumnCount)
            .Select(index => CreateExecutor(index, opcUaTagService, testLogger, loggerFactory, sequenseService))
            .ToArray();
    }

    private static ColumnExecutor CreateExecutor(
        int index,
        OpcUaTagService opcUa,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        TestSequenseService sequenseService)
    {
        var context = new TestStepContext(index, opcUa, loggerFactory.CreateLogger($"Column{index}"));
        var executorLogger = loggerFactory.CreateLogger<ColumnExecutor>();
        return new ColumnExecutor(index, context, testLogger, executorLogger, sequenseService);
    }

    private void SubscribeToExecutorEvents()
    {
        foreach (var executor in _executors)
        {
            executor.OnStateChanged += _onExecutorStateChanged;
        }
    }

    private void UnsubscribeFromExecutorEvents()
    {
        foreach (var executor in _executors)
        {
            executor.OnStateChanged -= _onExecutorStateChanged;
        }
    }

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
    }

    private void LogMapsLoaded(int count)
    {
        _logger.LogInformation("Загружено {Count} Maps", count);
        _testLogger.LogInformation("Загружена последовательность из {Count} блоков", count);
    }

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
        finally
        {
            Complete();
        }
    }

    private bool TryStart()
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Координатор уже выполняется");
                return false;
            }
            if (!HasMapsLoaded())
            {
                LogNoSequenceLoaded();
                return false;
            }
            BeginExecution();
            return true;
        }
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
        IsRunning = true;
        _cts = new CancellationTokenSource();
        _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
        _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
    }

    private async Task RunAllMaps()
    {
        for (CurrentMapIndex = 0; CurrentMapIndex < _maps.Count && !ShouldStop; CurrentMapIndex++)
        {
            await RunCurrentMap();
        }
    }

    private async Task RunCurrentMap()
    {
        var map = _maps[CurrentMapIndex];
        LogMapStart();
        await ExecuteMapOnAllColumns(map);
    }

    private void LogMapStart()
    {
        _logger.LogInformation("Map {Index}/{Total}", CurrentMapIndex + 1, _maps.Count);
        _testLogger.LogInformation("─── Блок {Index} из {Total} ───", CurrentMapIndex + 1, _maps.Count);
    }

    private async Task ExecuteMapOnAllColumns(TestMap map)
    {
        var executionTasks = _executors.Select(executor => executor.ExecuteMapAsync(map, _cts!.Token));
        await Task.WhenAll(executionTasks);
    }

    private void Complete()
    {
        lock (_stateLock)
        {
            IsRunning = false;
        }

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
        lock (_stateLock)
        {
            if (!IsRunning)
            {
                return;
            }

            LogStopRequested();
            _cts?.Cancel();
        }
    }

    private void LogStopRequested()
    {
        _logger.LogInformation("Остановка");
        _testLogger.LogWarning("Тестирование остановлено оператором");
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        UnsubscribeFromExecutorEvents();
    }
}
