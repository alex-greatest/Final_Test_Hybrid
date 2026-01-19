using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Управляет режимом сканирования: активация/деактивация на основе состояния оператора и автомата.
/// Scanner session управляется централизованно через подписку на SystemLifecycleManager.OnPhaseChanged.
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
    private readonly SystemLifecycleManager _lifecycle;
    private readonly ExecutionActivityTracker _activityTracker;
    private readonly DualLogger<ScanModeController> _logger;
    private CancellationTokenSource? _loopCts;
    private bool _disposed;
    public event Action? OnStateChanged;
    public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    /// <summary>
    /// Находится ли система в фазе сканирования.
    /// Используется PlcResetCoordinator для определения типа сброса.
    /// Мягкий сброс (ForceStop) — когда Phase == WaitingForBarcode.
    /// Жёсткий сброс (Reset) — в любой другой фазе.
    /// </summary>
    public bool IsInScanningPhase => _lifecycle.Phase == SystemPhase.WaitingForBarcode;

    public ScanModeController(
        ScanSessionManager sessionManager,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        StepStatusReporter statusReporter,
        PreExecutionCoordinator preExecutionCoordinator,
        PlcResetCoordinator plcResetCoordinator,
        IStepTimingService stepTimingService,
        SystemLifecycleManager lifecycle,
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
        _lifecycle = lifecycle;
        _activityTracker = activityTracker;
        _logger = logger;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        _lifecycle.OnPhaseChanged += HandlePhaseChanged;
        SubscribeToResetEvents();
        UpdateScanModeState();
    }

    private void SubscribeToResetEvents()
    {
        _plcResetCoordinator.OnResetStarting += HandleResetStarting;
        _plcResetCoordinator.OnResetCompleted += HandleResetCompleted;
    }

    /// <summary>
    /// Централизованный обработчик изменения фазы системы.
    /// Управляет scanner session на основе текущей фазы.
    /// </summary>
    private void HandlePhaseChanged(SystemPhase oldPhase, SystemPhase newPhase)
    {
        SynchronizeScannerSession(newPhase);
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Синхронизирует scanner session с текущей фазой системы.
    /// Гарантирует: IsScannerActive == true ↔ session acquired.
    /// </summary>
    private void SynchronizeScannerSession(SystemPhase phase)
    {
        var shouldHaveScanner = phase is SystemPhase.WaitingForBarcode or SystemPhase.Preparing;
        switch (shouldHaveScanner)
        {
            case true when !_sessionManager.HasActiveSession:
                _sessionManager.AcquireSession(HandleBarcodeScanned);
                break;
            case false when _sessionManager.HasActiveSession:
                _sessionManager.ReleaseSession();
                break;
        }
    }

    /// <summary>
    /// Обрабатывает начало сброса PLC.
    /// Возвращает true если система была в фазе сканирования (мягкий сброс),
    /// false если нет (жёсткий сброс). Значение используется PlcResetCoordinator
    /// для выбора метода сброса: ForceStop vs Reset.
    /// Scanner session освобождается автоматически через HandlePhaseChanged при переходе в Resetting.
    /// </summary>
    private bool HandleResetStarting()
    {
        var wasInScanPhase = IsInScanningPhase;
        var trigger = wasInScanPhase
            ? SystemTrigger.ResetRequestedSoft
            : SystemTrigger.ResetRequestedHard;
        _lifecycle.Transition(trigger);
        return wasInScanPhase;
    }

    /// <summary>
    /// Обрабатывает завершение сброса PLC.
    /// </summary>
    private void HandleResetCompleted()
    {
        TransitionToReadyInternal();
    }

    private void UpdateScanModeState()
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
        OnStateChanged?.Invoke();
    }

    /// <summary>
    /// Активирует режим сканирования. Два режима работы:
    /// 1) Первичная активация (Phase == Idle): запуск loop, таймеров, добавление шага в grid
    /// 2) Восстановление (Phase != Idle): только восстановление таймеров после возврата AutoMode.
    /// Scanner session управляется централизованно через HandlePhaseChanged.
    /// </summary>
    private void TryActivateScanMode()
    {
        var isFirstActivation = _lifecycle.Phase == SystemPhase.Idle;
        if (!isFirstActivation)
        {
            SynchronizeScannerSession(_lifecycle.Phase);
            _stepTimingService.ResumeAllColumnsTiming();
            return;
        }
        _lifecycle.Transition(SystemTrigger.ScanModeEnabled);
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

    /// <summary>
    /// Деактивирует режим сканирования. Три режима работы:
    /// 1) Blocked (Resetting/Idle/Completed): игнорируем — управление через reset или completion flow
    /// 2) Soft (Preparing/Testing): только пауза таймеров (тест продолжает выполняться)
    /// 3) Hard (WaitingForBarcode): полная деактивация с отменой loop.
    /// Scanner session управляется централизованно через HandlePhaseChanged.
    /// </summary>
    private void TryDeactivateScanMode()
    {
        var phase = _lifecycle.Phase;
        switch (phase)
        {
            case SystemPhase.Resetting:
            case SystemPhase.Idle:
            case SystemPhase.Completed:
                // В Completed ждём действий оператора — AutoReady не влияет
                return;
        }
        if (phase is SystemPhase.Preparing or SystemPhase.Testing)
        {
            SynchronizeScannerSession(phase);
            _stepTimingService.PauseAllColumnsTiming();
            return;
        }
        _loopCts?.Cancel();
        _lifecycle.Transition(SystemTrigger.ScanModeDisabled);
        if (!_operatorState.IsAuthenticated)
        {
            _statusReporter.ClearAllExceptScan();
        }
    }

    /// <summary>
    /// Переводит систему в состояние готовности после завершения сброса.
    /// Scanner session управляется централизованно через HandlePhaseChanged.
    /// </summary>
    private void TransitionToReadyInternal()
    {
        if (_lifecycle.Phase != SystemPhase.Resetting)
        {
            return;
        }
        if (!IsScanModeEnabled)
        {
            _loopCts?.Cancel();
            _lifecycle.Transition(SystemTrigger.ResetCompletedHard);
            return;
        }
        _lifecycle.Transition(SystemTrigger.ResetCompletedSoft);
        _stepTimingService.ResetScanTiming();
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
        _lifecycle.OnPhaseChanged -= HandlePhaseChanged;
        _plcResetCoordinator.OnResetStarting -= HandleResetStarting;
        _plcResetCoordinator.OnResetCompleted -= HandleResetCompleted;
    }
}
