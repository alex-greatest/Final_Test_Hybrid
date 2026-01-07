using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator : IDisposable
{
    private const int ColumnCount = 4;
    private readonly ColumnExecutor[] _executors;
    private readonly ILogger<TestExecutionCoordinator> _logger;
    private readonly ITestStepLogger _testLogger;
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
    public bool IsRunning => StateManager.State == ExecutionState.Running;
    public bool HasErrors => StateManager.HasErrors;
    public ExecutionStateManager StateManager { get; }

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
        StateManager = stateManager;
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

    public void Dispose()
    {
        _errorResolutionTcs?.TrySetCanceled();
        Stop();
        _cts?.Dispose();
        UnsubscribeFromExecutorEvents();
        UnsubscribeFromErrorHandler();
    }
}
