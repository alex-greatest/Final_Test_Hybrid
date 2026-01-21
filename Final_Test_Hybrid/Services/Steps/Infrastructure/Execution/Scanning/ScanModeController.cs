using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Управляет режимом сканирования: активация/деактивация на основе состояния оператора и автомата.
/// Координирует управление сессией сканера и уведомляет подписчиков об изменении состояния.
/// </summary>
public class ScanModeController : IDisposable
{
    private readonly ScanSessionManager _sessionManager;
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly StepStatusReporter _statusReporter;
    private readonly PreExecutionCoordinator _preExecutionCoordinator;
    private readonly PlcResetCoordinator _plcResetCoordinator;
    private readonly IStepTimingService _stepTimingService;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly DualLogger<ScanModeController> _logger;
    private readonly Lock _stateLock = new();
    private CancellationTokenSource? _loopCts;
    /// <summary>
    /// Режим сканирования активирован (оператор авторизован + AutoReady).
    /// </summary>
    private bool _isActivated;
    /// <summary>
    /// Выполняется сброс PLC (от Req_Reset сигнала).
    /// </summary>
    private bool _isResetting;
    private bool _disposed;

    public event Action? OnStateChanged;

    public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    /// <summary>
    /// Внутренняя проверка без блокировки - использовать только внутри lock(_stateLock).
    /// </summary>
    private bool IsInScanningPhaseUnsafe => _isActivated && !_isResetting;

    /// <summary>
    /// Находится ли система в фазе сканирования (активирована, но не в режиме сброса).
    /// Используется PlcResetCoordinator для определения типа сброса.
    /// Thread-safe для внешних вызовов.
    /// </summary>
    public bool IsInScanningPhase
    {
        get
        {
            lock (_stateLock)
            {
                return IsInScanningPhaseUnsafe;
            }
        }
    }

    public ScanModeController(
        ScanSessionManager sessionManager,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        StepStatusReporter statusReporter,
        PreExecutionCoordinator preExecutionCoordinator,
        PlcResetCoordinator plcResetCoordinator,
        IStepTimingService stepTimingService,
        ExecutionActivityTracker activityTracker,
        DualLogger<ScanModeController> logger)
    {
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _statusReporter = statusReporter;
        _preExecutionCoordinator = preExecutionCoordinator;
        _plcResetCoordinator = plcResetCoordinator;
        _stepTimingService = stepTimingService;
        _activityTracker = activityTracker;
        _logger = logger;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        SubscribeToResetEvents();
        UpdateScanModeState();
    }

    private void SubscribeToResetEvents()
    {
        _plcResetCoordinator.OnResetStarting += HandleResetStarting;
        _plcResetCoordinator.OnResetCompleted += HandleResetCompleted;
    }

    /// <summary>
    /// Обрабатывает начало сброса PLC.
    /// Возвращает true если система была в фазе сканирования (мягкий сброс),
    /// false если нет (жёсткий сброс). Значение используется PlcResetCoordinator
    /// для выбора метода сброса: ForceStop vs Reset.
    /// </summary>
    private bool HandleResetStarting()
    {
        lock (_stateLock)
        {
            var wasInScanPhase = IsInScanningPhaseUnsafe;
            _isResetting = true;
            _stepTimingService.PauseAllColumnsTiming();
            _sessionManager.ReleaseSession();
            return wasInScanPhase;
        }
    }

    private void HandleResetCompleted()
    {
        lock (_stateLock)
        {
            TransitionToReadyInternal();
        }
    }

    private void UpdateScanModeState()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }
            if (IsScanModeEnabled)
            {
                TryActivateScanMode();
            }
            else
            {
                TryDeactivateScanMode();
            }
        }
        OnStateChanged?.Invoke();
    }

    private void TryActivateScanMode()
    {
        if (_isResetting)
        {
            return;
        }
        if (_isActivated)
        {
            RefreshSessionAndTimingForActiveMode();
            return;
        }
        PerformInitialActivation();
    }

    /// <summary>
    /// Пере-захватывает сессию и возобновляет таймеры для уже активного режима.
    /// </summary>
    private void RefreshSessionAndTimingForActiveMode()
    {
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        _stepTimingService.ResumeAllColumnsTiming();
    }

    /// <summary>
    /// Выполняет первичную активацию режима сканирования.
    /// </summary>
    private void PerformInitialActivation()
    {
        _isActivated = true;
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        AddScanStepToGrid();
        StartScanTiming();
        StartMainLoop();
    }

    private void StartScanTiming()
    {
        var scanStep = _preExecutionCoordinator.GetScanStep();
        _stepTimingService.StartScanTiming(scanStep.Name, scanStep.Description);
    }

    private void AddScanStepToGrid()
    {
        var scanStep = _preExecutionCoordinator.GetScanStep();
        _statusReporter.EnsureScanStepExists(scanStep.Name, scanStep.Description);
    }

    private void StartMainLoop()
    {
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _loopCts = new CancellationTokenSource();
        _ = _preExecutionCoordinator.StartMainLoopAsync(_loopCts.Token)
            .ContinueWith(
                t => _logger.LogError(t.Exception, "Main loop failed"),
                TaskContinuationOptions.OnlyOnFaulted);
    }

    private void HandleBarcodeScanned(string barcode)
    {
        _preExecutionCoordinator.SubmitBarcode(barcode);
    }

    private void TryDeactivateScanMode()
    {
        if (_isResetting)
        {
            return;
        }
        if (!_isActivated)
        {
            return;
        }

        _stepTimingService.PauseAllColumnsTiming();

        if (ShouldUseSoftDeactivation())
        {
            PerformSoftDeactivation();
            return;
        }
        PerformFullDeactivation();
    }

    /// <summary>
    /// Определяет необходимость мягкой деактивации.
    /// </summary>
    private bool ShouldUseSoftDeactivation()
    {
        var isAutoModeLost = _operatorState.IsAuthenticated && !_autoReady.IsReady;
        var isExecutionActive = _activityTracker.IsAnyActive;
        var isWaitingForScan = _operatorState.IsAuthenticated && _preExecutionCoordinator.IsAcceptingInput;
        return isAutoModeLost || isExecutionActive || isWaitingForScan;
    }

    /// <summary>
    /// Выполняет мягкую деактивацию режима сканирования.
    /// </summary>
    private void PerformSoftDeactivation()
    {
        _sessionManager.ReleaseSession();
    }

    /// <summary>
    /// Выполняет полную деактивацию режима сканирования.
    /// </summary>
    private void PerformFullDeactivation()
    {
        _loopCts?.Cancel();
        _isActivated = false;
        _sessionManager.ReleaseSession();
        if (!_operatorState.IsAuthenticated)
        {
            _statusReporter.ClearAllExceptScan();
        }
    }

    private void TransitionToReadyInternal()
    {
        _isResetting = false;
        if (!IsScanModeEnabled)
        {
            _loopCts?.Cancel();
            _isActivated = false;
            _stepTimingService.PauseAllColumnsTiming();
            return;
        }
        if (!_isActivated)
        {
            PerformInitialActivation();
            return;
        }
        _stepTimingService.ResetScanTiming();
        _sessionManager.AcquireSession(HandleBarcodeScanned);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _loopCts?.Cancel();
        _loopCts?.Dispose();
        _operatorState.OnStateChanged -= UpdateScanModeState;
        _autoReady.OnStateChanged -= UpdateScanModeState;
        _plcResetCoordinator.OnResetStarting -= HandleResetStarting;
        _plcResetCoordinator.OnResetCompleted -= HandleResetCompleted;
    }
}
