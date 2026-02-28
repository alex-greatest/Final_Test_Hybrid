using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Connection;
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
    private readonly OpcUaConnectionState _connectionState;
    private readonly StepStatusReporter _statusReporter;
    private readonly BarcodeDebounceHandler _barcodeDebounceHandler;
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
    private bool _scanPausedByInputReadiness;
    /// <summary>
    /// Кэшированное состояние AutoReady. Обновляется только под _stateLock.
    /// Используется для внутренних решений чтобы избежать race condition.
    /// </summary>
    private bool _cachedIsAutoReady;

    public event Action? OnStateChanged;

    public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    /// <summary>
    /// Внутренняя проверка режима сканирования (использует кэш).
    /// Использовать только внутри lock(_stateLock).
    /// </summary>
    private bool IsScanModeEnabledCached => _operatorState.IsAuthenticated && _cachedIsAutoReady;

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

    /// <summary>
    /// Инициализирует контроллер режима сканирования с необходимыми зависимостями.
    /// </summary>
    public ScanModeController(
        ScanSessionManager sessionManager,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        OpcUaConnectionState connectionState,
        StepStatusReporter statusReporter,
        BarcodeDebounceHandler barcodeDebounceHandler,
        PreExecutionCoordinator preExecutionCoordinator,
        PlcResetCoordinator plcResetCoordinator,
        IStepTimingService stepTimingService,
        ExecutionActivityTracker activityTracker,
        DualLogger<ScanModeController> logger)
    {
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _connectionState = connectionState;
        _statusReporter = statusReporter;
        _barcodeDebounceHandler = barcodeDebounceHandler;
        _preExecutionCoordinator = preExecutionCoordinator;
        _plcResetCoordinator = plcResetCoordinator;
        _stepTimingService = stepTimingService;
        _activityTracker = activityTracker;
        _logger = logger;
        SubscribeToEvents();
    }

    /// <summary>
    /// Подписывается на все необходимые события состояния оператора и автомата.
    /// </summary>
    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        _connectionState.ConnectionStateChanged += HandleConnectionStateChanged;
        _preExecutionCoordinator.OnStateChanged += HandlePreExecutionStateChanged;
        SubscribeToResetEvents();
        UpdateScanModeState();
    }

    /// <summary>
    /// Подписывается на события сброса PLC.
    /// </summary>
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
            _scanPausedByInputReadiness = false;
            _stepTimingService.PauseAllColumnsTiming();
            _sessionManager.ReleaseSession();
            return wasInScanPhase;
        }
    }

    /// <summary>
    /// Обрабатывает завершение сброса PLC, переводя систему в состояние готовности.
    /// </summary>
    private void HandleResetCompleted()
    {
        lock (_stateLock)
        {
            _cachedIsAutoReady = _autoReady.IsReady;  // Обновляем снимок перед решениями
            TransitionToReadyInternal();
        }
    }

    /// <summary>
    /// Обновляет состояние режима сканирования на основе текущих условий.
    /// </summary>
    private void UpdateScanModeState()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _cachedIsAutoReady = _autoReady.IsReady;  // Обновляем снимок под lock

            if (IsScanModeEnabledCached)
            {
                TryActivateScanMode();
            }
            else
            {
                TryDeactivateScanMode();
            }

            SyncScanTimingForInputReadinessUnsafe();
        }
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Пытается активировать режим сканирования, если условия позволяют.
    /// </summary>
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
    /// Перезахватывает сессию и возобновляет таймеры для уже активного режима.
    /// </summary>
    private void RefreshSessionAndTimingForActiveMode()
    {
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        ResumeTimingWhenAllowedUnsafe();
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

    /// <summary>
    /// Запускает таймер для шага сканирования.
    /// </summary>
    private void StartScanTiming()
    {
        var scanStep = _preExecutionCoordinator.GetScanStep();
        _stepTimingService.StartScanTiming(scanStep.Name, scanStep.Description);
    }

    /// <summary>
    /// Добавляет шаг сканирования в сетку статусов.
    /// </summary>
    private void AddScanStepToGrid()
    {
        var scanStep = _preExecutionCoordinator.GetScanStep();
        _statusReporter.EnsureScanStepExists(scanStep.Name, scanStep.Description);
    }

    /// <summary>
    /// Запускает основной цикл обработки штрих-кодов в фоновом режиме.
    /// </summary>
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

    /// <summary>
    /// Обрабатывает отсканированный штрих-код, передавая его координатору.
    /// </summary>
    private void HandleBarcodeScanned(string barcode)
    {
        _barcodeDebounceHandler.Handle(barcode);
    }

    /// <summary>
    /// Пытается деактивировать режим сканирования с выбором мягкой или полной деактивации.
    /// </summary>
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
        var isAutoModeLost = _operatorState.IsAuthenticated && !_cachedIsAutoReady;
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
        if (!_operatorState.IsAuthenticated && !_plcResetCoordinator.IsActive)
        {
            _statusReporter.ClearAllExceptScan();
        }
    }

    /// <summary>
    /// Выполняет переход в состояние готовности после завершения сброса.
    /// </summary>
    private void TransitionToReadyInternal()
    {
        _isResetting = false;
        if (!IsScanModeEnabledCached)
        {
            _loopCts?.Cancel();
            _isActivated = false;
            _scanPausedByInputReadiness = false;
            _stepTimingService.PauseAllColumnsTiming();
            return;
        }
        if (!_isActivated)
        {
            _scanPausedByInputReadiness = false;
            PerformInitialActivation();
            SyncScanTimingForInputReadinessUnsafe();
            return;
        }
        if (ShouldKeepScanPausedForInputUnsafe())
        {
            LogScanTimingRestartBlockedByInterruptDialog();
        }
        else
        {
            _stepTimingService.ResetScanTiming();
        }
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        SyncScanTimingForInputReadinessUnsafe();
    }

    private void HandleConnectionStateChanged(bool _)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }
            SyncScanTimingForInputReadinessUnsafe();
        }
    }

    private void HandlePreExecutionStateChanged()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }
            SyncScanTimingForInputReadinessUnsafe();
        }
    }

    private void ResumeTimingWhenAllowedUnsafe()
    {
        if (ShouldKeepScanPausedForInputUnsafe())
        {
            return;
        }
        _stepTimingService.ResumeAllColumnsTiming();
        _scanPausedByInputReadiness = false;
    }

    private bool ShouldKeepScanPausedForInputUnsafe()
    {
        if (_preExecutionCoordinator.IsInterruptReasonDialogActive())
        {
            return true;
        }
        if (!_preExecutionCoordinator.IsAcceptingInput)
        {
            return false;
        }
        return !_connectionState.IsConnected || !IsScanModeEnabledCached;
    }

    private void SyncScanTimingForInputReadinessUnsafe()
    {
        if (ShouldKeepScanPausedForInputUnsafe())
        {
            TryPauseScanTimingForInputReadinessUnsafe();
            return;
        }

        if (!_preExecutionCoordinator.IsAcceptingInput)
        {
            _scanPausedByInputReadiness = false;
            return;
        }

        if (!CanEnsureScanTimingForInputReadinessUnsafe())
        {
            return;
        }

        TryResumeScanTimingAfterInputReadyUnsafe();
        _stepTimingService.ResetScanTiming();
    }

    private bool CanEnsureScanTimingForInputReadinessUnsafe()
    {
        if (!IsScanModeEnabledCached || !_connectionState.IsConnected || _isResetting || !_isActivated)
        {
            return false;
        }
        return !_preExecutionCoordinator.IsInterruptReasonDialogActive();
    }

    private void TryPauseScanTimingForInputReadinessUnsafe()
    {
        if (_scanPausedByInputReadiness)
        {
            return;
        }
        _stepTimingService.PauseAllColumnsTiming();
        _scanPausedByInputReadiness = true;
    }

    private void TryResumeScanTimingAfterInputReadyUnsafe()
    {
        if (!_scanPausedByInputReadiness || !IsScanModeEnabledCached || !_connectionState.IsConnected || _isResetting || !_isActivated || ShouldKeepScanPausedForInputUnsafe())
        {
            return;
        }
        _stepTimingService.ResumeAllColumnsTiming();
        _scanPausedByInputReadiness = false;
    }

    private void LogScanTimingRestartBlockedByInterruptDialog()
    {
        _logger.LogInformation(
            "ScanTimingRestartBlockedByInterruptDialog: currentSeq={CurrentSequence}, dialogSeq={DialogSequence}",
            _preExecutionCoordinator.GetCurrentResetSequenceSnapshot(),
            _preExecutionCoordinator.GetInterruptReasonDialogSequenceSnapshot());
    }

    /// <summary>
    /// Освобождает ресурсы и отписывается от всех событий.
    /// </summary>
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
        _connectionState.ConnectionStateChanged -= HandleConnectionStateChanged;
        _preExecutionCoordinator.OnStateChanged -= HandlePreExecutionStateChanged;
        _plcResetCoordinator.OnResetStarting -= HandleResetStarting;
        _plcResetCoordinator.OnResetCompleted -= HandleResetCompleted;
    }
}
