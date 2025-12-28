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
    private bool ShouldStop => _cts?.IsCancellationRequested == true || HasErrors;

    public TestExecutionCoordinator(
        OpcUaTagService opcUaTagService,
        ILogger<TestExecutionCoordinator> logger,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _testLogger = testLogger;
        _onExecutorStateChanged = () => OnStateChanged?.Invoke();
        _executors = Enumerable.Range(0, ColumnCount)
            .Select(i => CreateExecutor(i, opcUaTagService, testLogger, loggerFactory))
            .ToArray();

        foreach (var executor in _executors)
        {
            executor.OnStateChanged += _onExecutorStateChanged;
        }
    }

    private static ColumnExecutor CreateExecutor(
        int index,
        OpcUaTagService opcUa,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory)
    {
        var context = new TestStepContext(index, opcUa, loggerFactory.CreateLogger($"Column{index}"));
        return new ColumnExecutor(index, context, testLogger, loggerFactory.CreateLogger<ColumnExecutor>());
    }

    public void SetMaps(List<TestMap> maps)
    {
        lock (_stateLock)
        {
            if (IsRunning)
            {
                _logger.LogWarning("Попытка загрузить Maps во время выполнения");
                return;
            }
            _maps = [..maps];
            CurrentMapIndex = 0;
            foreach (var executor in _executors)
            {
                executor.Reset();
            }
            _logger.LogInformation("Загружено {Count} Maps", maps.Count);
            _testLogger.LogInformation("Загружена последовательность из {Count} блоков", maps.Count);
        }
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
            if (_maps.Count == 0)
            {
                _logger.LogError("Последовательность не загружена");
                _testLogger.LogError(null, "Ошибка: последовательность тестов не загружена");
                return false;
            }
            IsRunning = true;
            _cts = new CancellationTokenSource();
            _logger.LogInformation("Запуск {Count} Maps", _maps.Count);
            _testLogger.LogInformation("═══ ЗАПУСК ТЕСТИРОВАНИЯ ({Count} блоков) ═══", _maps.Count);
            return true;
        }
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
        _logger.LogInformation("Map {Index}/{Total}", CurrentMapIndex + 1, _maps.Count);
        _testLogger.LogInformation("─── Блок {Index} из {Total} ───", CurrentMapIndex + 1, _maps.Count);
        await Task.WhenAll(_executors.Select(e => e.ExecuteMapAsync(map, _cts!.Token)));
    }

    private void Complete()
    {
        lock (_stateLock)
        {
            IsRunning = false;
        }
        _logger.LogInformation("Завершено. Ошибки: {HasErrors}", HasErrors);
        var result = HasErrors ? "С ОШИБКАМИ" : "УСПЕШНО";
        _testLogger.LogInformation("═══ ТЕСТИРОВАНИЕ ЗАВЕРШЕНО: {Result} ═══", result);
        OnSequenceCompleted?.Invoke();
    }

    public void Stop()
    {
        lock (_stateLock)
        {
            if (!IsRunning)
            {
                return;
            }
            _logger.LogInformation("Остановка");
            _testLogger.LogWarning("Тестирование остановлено оператором");
            _cts?.Cancel();
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        foreach (var executor in _executors)
        {
            executor.OnStateChanged -= _onExecutorStateChanged;
        }
    }
}
