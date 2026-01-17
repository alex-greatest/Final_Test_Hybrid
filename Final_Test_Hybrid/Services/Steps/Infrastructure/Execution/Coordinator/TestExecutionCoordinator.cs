using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator : IDisposable
{
    private const int ColumnCount = 4;
    private readonly ColumnExecutor[] _executors;
    private readonly ILogger<TestExecutionCoordinator> _logger;
    private readonly ITestStepLogger _testLogger;
    private readonly IErrorCoordinator _errorCoordinator;
    private readonly OpcUaTagService _plcService;
    private readonly PauseTokenSource _pauseToken;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly IErrorService _errorService;
    private readonly IStepTimingService _stepTimingService;
    private readonly TagWaiter _tagWaiter;
    private readonly Lock _stateLock = new();
    private readonly object _enqueueLock = new();
    private readonly Action _onExecutorStateChanged;
    private List<TestMap> _maps = [];
    private CancellationTokenSource? _cts;
    public event Action? OnStateChanged;
    public event Action? OnSequenceCompleted;
    public event Action<StepError>? OnErrorOccurred;
    public IReadOnlyList<ColumnExecutor> Executors => _executors;
    public int CurrentMapIndex { get; private set; }
    public int TotalMaps => _maps.Count;
    public bool IsRunning => StateManager.State == ExecutionState.Running;
    public bool HasErrors => StateManager.HasPendingErrors;
    public bool HadSkippedError => StateManager.HadSkippedError;
    public ExecutionStateManager StateManager { get; }

    private bool ShouldStop => IsCancellationRequested;
    private bool IsCancellationRequested => _cts?.IsCancellationRequested == true;

    public TestExecutionCoordinator(
        OpcUaTagService plcService,
        PausableOpcUaTagService pausableOpcUaTagService,
        ILogger<TestExecutionCoordinator> logger,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider,
        ExecutionStateManager stateManager,
        IErrorCoordinator errorCoordinator,
        PauseTokenSource pauseToken,
        ExecutionActivityTracker activityTracker,
        PlcResetCoordinator plcResetCoordinator,
        IErrorService errorService,
        IStepTimingService stepTimingService,
        TagWaiter tagWaiter)
    {
        _logger = logger;
        _testLogger = testLogger;
        StateManager = stateManager;
        _errorCoordinator = errorCoordinator;
        _plcService = plcService;
        _pauseToken = pauseToken;
        _activityTracker = activityTracker;
        _plcResetCoordinator = plcResetCoordinator;
        _errorService = errorService;
        _stepTimingService = stepTimingService;
        _tagWaiter = tagWaiter;
        _onExecutorStateChanged = HandleExecutorStateChanged;
        _executors = CreateAllExecutors(pausableOpcUaTagService, testLogger, loggerFactory, statusReporter, recipeProvider);
        SubscribeToExecutorEvents();
        _plcResetCoordinator.OnForceStop += HandleForceStop;
        _errorCoordinator.OnReset += HandleReset;
    }

    private void HandleForceStop()
    {
        Stop("по сигналу PLC");
        StateManager.ClearErrors();
    }

    private void HandleReset()
    {
        Stop("из-за полного сброса");
        StateManager.ClearErrors();
    }

    public void ResetForRepeat()
    {
        StateManager.ClearErrors();
        StateManager.ResetErrorTracking();
        StateManager.TransitionTo(ExecutionState.Idle);

        // Сбросить состояние всех executor'ов
        foreach (var executor in _executors)
        {
            executor.ClearFailedState();
        }
    }

    private ColumnExecutor[] CreateAllExecutors(
        PausableOpcUaTagService opcUaTagService,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider)
    {
        return Enumerable.Range(0, ColumnCount)
            .Select(index => CreateExecutor(index, opcUaTagService, testLogger, loggerFactory, statusReporter, recipeProvider, _pauseToken, _errorService, _stepTimingService))
            .ToArray();
    }

    private static ColumnExecutor CreateExecutor(
        int index,
        PausableOpcUaTagService opcUa,
        ITestStepLogger testLogger,
        ILoggerFactory loggerFactory,
        StepStatusReporter statusReporter,
        IRecipeProvider recipeProvider,
        PauseTokenSource pauseToken,
        IErrorService errorService,
        IStepTimingService stepTimingService)
    {
        var context = new TestStepContext(index, opcUa, loggerFactory.CreateLogger($"Column{index}"), recipeProvider);
        var executorLogger = loggerFactory.CreateLogger<ColumnExecutor>();
        return new ColumnExecutor(index, context, testLogger, executorLogger, statusReporter, pauseToken, errorService, stepTimingService);
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

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        UnsubscribeFromExecutorEvents();
        _plcResetCoordinator.OnForceStop -= HandleForceStop;
        _errorCoordinator.OnReset -= HandleReset;
    }
}