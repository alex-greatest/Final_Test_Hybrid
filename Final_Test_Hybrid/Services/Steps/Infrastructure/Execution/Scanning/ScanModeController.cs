using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

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
    private readonly Lock _stateLock = new();
    private CancellationTokenSource? _loopCts;
    private bool _isActivated;
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
        ExecutionActivityTracker activityTracker)
    {
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _statusReporter = statusReporter;
        _preExecutionCoordinator = preExecutionCoordinator;
        _plcResetCoordinator = plcResetCoordinator;
        _stepTimingService = stepTimingService;
        _activityTracker = activityTracker;
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
        _plcResetCoordinator.OnResetStarting += () =>
        {
            lock (_stateLock)
            {
                var wasInScanPhase = IsInScanningPhaseUnsafe;
                _isResetting = true;
                _sessionManager.ReleaseSession();
                return wasInScanPhase;
            }
        };

        _plcResetCoordinator.OnResetCompleted += () =>
        {
            lock (_stateLock)
            {
                TransitionToReadyInternal();
            }
        };
    }

    private void UpdateScanModeState()
    {
        lock (_stateLock)
        {
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
        if (_isActivated)
        {
            // Восстановить сканер и таймер при возврате AutoMode
            _sessionManager.AcquireSession(HandleBarcodeScanned);
            _stepTimingService.ResumeAllColumnsTiming();
            return;
        }
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
        _ = _preExecutionCoordinator.StartMainLoopAsync(_loopCts.Token);
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
        // Если тест выполняется — только отключить сканер и приостановить таймеры всех колонок
        if (_activityTracker.IsAnyActive)
        {
            _sessionManager.ReleaseSession();
            _stepTimingService.PauseAllColumnsTiming();
            return;
        }
        // Отменять loop только если тест НЕ активен
        _loopCts?.Cancel();
        _isActivated = false;
        _sessionManager.ReleaseSession();
        if (!_operatorState.IsAuthenticated)
        {
            _statusReporter.ClearAllExceptScan();
        }
    }

    private void TransitionToReady()
    {
        lock (_stateLock)
        {
            TransitionToReadyInternal();
        }
    }

    private void TransitionToReadyInternal()
    {
        _isResetting = false;
        if (!IsScanModeEnabled)
        {
            _loopCts?.Cancel();
            _isActivated = false;
            return;
        }
        _stepTimingService.ResetScanTiming();
        _sessionManager.AcquireSession(HandleBarcodeScanned);
    }

    /// <summary>
    /// Входит в режим сброса — блокирует ввод сканера до завершения сброса.
    /// </summary>
    private void EnterResettingMode()
    {
        _isResetting = true;
        _sessionManager.ReleaseSession();
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
    }
}
