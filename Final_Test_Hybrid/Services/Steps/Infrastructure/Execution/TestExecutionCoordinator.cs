using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class TestExecutionCoordinator : IDisposable
{
    private const int ColumnCount = 4;
    private readonly ColumnExecutor[] _executors;
    private readonly ILogger<TestExecutionCoordinator> _logger;
    private readonly ITestStepLogger _testLogger;
    private readonly ExecutionStateManager _stateManager;
    private readonly StepErrorHandler _errorHandler;
    private readonly PauseTokenSource _pauseToken;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly Lock _stateLock = new();
    private readonly Action _onExecutorStateChanged;
    private List<TestMap> _maps = [];
    private CancellationTokenSource? _cts;
    private TaskCompletionSource<ErrorResolution>? _errorResolutionTcs;
    public event Action? OnStateChanged;
    public event Action? OnSequenceCompleted;
    public event Action<StepError>? OnErrorOccurred;
    public IReadOnlyList<ColumnExecutor> Executors => _executors;
    public int CurrentMapIndex { get; private set; }
    public int TotalMaps => _maps.Count;
    public bool IsRunning => _stateManager.State == ExecutionState.Running;
    public bool HasErrors => _executors.Any(e => e.HasFailed);
    public ExecutionStateManager StateManager => _stateManager;

    private bool ShouldStop => IsCancellationRequested;
    private bool IsCancellationRequested => _cts?.IsCancellationRequested == true;

    public TestExecutionCoordinator(
        OpcUaTagService opcUaTagService,
        ILogger<TestExecutionCoordinator> logger,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider,
        ExecutionStateManager stateManager,
        StepErrorHandler errorHandler,
        PauseTokenSource pauseToken,
        ExecutionActivityTracker activityTracker)
    {
        _logger = logger;
        _testLogger = testLogger;
        _stateManager = stateManager;
        _errorHandler = errorHandler;
        _pauseToken = pauseToken;
        _activityTracker = activityTracker;
        _onExecutorStateChanged = HandleExecutorStateChanged;
        _executors = CreateAllExecutors(opcUaTagService, testLogger, loggerFactory, statusReporter, recipeProvider);
        SubscribeToExecutorEvents();
        SubscribeToErrorHandler();
    }

    private ColumnExecutor[] CreateAllExecutors(
        OpcUaTagService opcUaTagService,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider)
    {
        return Enumerable.Range(0, ColumnCount)
            .Select(index => CreateExecutor(index, opcUaTagService, testLogger, loggerFactory, statusReporter, recipeProvider, _pauseToken))
            .ToArray();
    }

    private static ColumnExecutor CreateExecutor(
        int index,
        OpcUaTagService opcUa,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider,
        PauseTokenSource pauseToken)
    {
        var context = new TestStepContext(index, opcUa, loggerFactory.CreateLogger($"Column{index}"), recipeProvider);
        var executorLogger = loggerFactory.CreateLogger<ColumnExecutor>();
        return new ColumnExecutor(index, context, testLogger, executorLogger, statusReporter, pauseToken);
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

    private void SubscribeToErrorHandler()
    {
        _errorHandler.OnResolutionReceived += HandleErrorResolution;
    }

    private void UnsubscribeFromErrorHandler()
    {
        _errorHandler.OnResolutionReceived -= HandleErrorResolution;
    }

    private void HandleExecutorStateChanged()
    {
        OnStateChanged?.Invoke();
        CheckForErrors();
    }

    private void CheckForErrors()
    {
        var failedExecutor = GetFirstFailedExecutorIfRunning();
        if (failedExecutor == null)
        {
            return;
        }
        ReportError(failedExecutor);
    }

    private ColumnExecutor? GetFirstFailedExecutorIfRunning()
    {
        if (_stateManager.State != ExecutionState.Running)
        {
            return null;
        }
        return _executors.FirstOrDefault(e => e.HasFailed);
    }

    private void ReportError(ColumnExecutor executor)
    {
        var error = new StepError(
            executor.ColumnIndex,
            executor.CurrentStepName ?? "Неизвестный шаг",
            executor.CurrentStepDescription ?? "",
            executor.ErrorMessage ?? "Неизвестная ошибка",
            DateTime.Now,
            Guid.Empty);
        _stateManager.TransitionTo(ExecutionState.PausedOnError, error);
        lock (_stateLock)
        {
            _errorResolutionTcs = new TaskCompletionSource<ErrorResolution>();
        }
        OnErrorOccurred?.Invoke(error);
    }

    private void HandleErrorResolution(ErrorResolution resolution)
    {
        lock (_stateLock)
        {
            _errorResolutionTcs?.TrySetResult(resolution);
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
        if (_stateManager.IsActive)
        {
            _logger.LogWarning("Координатор уже выполняется");
            return false;
        }
        return ValidateMapsLoaded();
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
        _stateManager.TransitionTo(ExecutionState.Running);
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

    private async Task HandleErrorsIfAny()
    {
        while (ShouldContinueErrorHandling())
        {
            var resolution = await WaitForErrorResolution();
            await ProcessErrorResolution(resolution);
        }
    }

    private bool ShouldContinueErrorHandling()
    {
        return HasErrors && !IsCancellationRequested;
    }

    private async Task<ErrorResolution> WaitForErrorResolution()
    {
        Task<ErrorResolution>? taskToAwait;
        lock (_stateLock)
        {
            taskToAwait = _errorResolutionTcs?.Task;
        }
        if (taskToAwait == null)
        {
            return ErrorResolution.None;
        }
        return await taskToAwait;
    }

    private async Task ProcessErrorResolution(ErrorResolution resolution)
    {
        switch (resolution)
        {
            case ErrorResolution.Retry:
                await RetryFailedSteps();
                break;
            case ErrorResolution.Skip:
                SkipFailedSteps();
                break;
            case ErrorResolution.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, null);
        }
    }

    private async Task RetryFailedSteps()
    {
        var failedExecutors = _executors.Where(e => e.HasFailed).ToList();
        var retryTasks = failedExecutors.Select(e => e.RetryLastFailedStepAsync(_cts!.Token));
        await Task.WhenAll(retryTasks);
    }

    private void SkipFailedSteps()
    {
        foreach (var executor in _executors.Where(e => e.HasFailed))
        {
            executor.ClearFailedState();
        }
        _stateManager.TransitionTo(ExecutionState.Running);
    }

    private void Complete()
    {
        var finalState = HasErrors ? ExecutionState.Failed : ExecutionState.Completed;
        _stateManager.TransitionTo(finalState);
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
        CancellationTokenSource? ctsToCancel = null;
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

    public void Dispose()
    {
        _errorResolutionTcs?.TrySetCanceled();
        Stop();
        _cts?.Dispose();
        UnsubscribeFromExecutorEvents();
        UnsubscribeFromErrorHandler();
    }
}
