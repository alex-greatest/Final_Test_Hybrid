using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

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
    private readonly MessageService _messageService;
    private readonly StepStatusReporter _statusReporter;
    private readonly PreExecutionCoordinator _preExecutionCoordinator;
    private CancellationTokenSource? _loopCts;
    private bool _isActivated;
    private bool _isResetting;
    private bool _disposed;

    public event Action? OnStateChanged;

    public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    /// <summary>
    /// Находится ли система в фазе сканирования (активирована, но не в режиме сброса).
    /// Используется PlcResetCoordinator для определения типа сброса.
    /// </summary>
    public bool IsInScanningPhase => _isActivated && !_isResetting;

    public ScanModeController(
        ScanSessionManager sessionManager,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        MessageService messageService,
        StepStatusReporter statusReporter,
        PreExecutionCoordinator preExecutionCoordinator)
    {
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _messageService = messageService;
        _statusReporter = statusReporter;
        _preExecutionCoordinator = preExecutionCoordinator;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        UpdateScanModeState();
    }

    private void UpdateScanModeState()
    {
        if (IsScanModeEnabled)
        {
            TryActivateScanMode();
        }
        else
        {
            TryDeactivateScanMode();
        }
        _messageService.NotifyChanged();
        OnStateChanged?.Invoke();
    }

    private void TryActivateScanMode()
    {
        if (_isActivated)
        {
            return;
        }
        _isActivated = true;
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        AddScanStepToGrid();
        StartMainLoop();
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
        _loopCts?.Cancel();
        if (_isResetting)
        {
            return;
        }
        if (!_isActivated)
        {
            return;
        }
        _isActivated = false;
        _sessionManager.ReleaseSession();
        if (!_operatorState.IsAuthenticated)
        {
            _statusReporter.ClearAll();
        }
    }

    public void TransitionToReady()
    {
        _isResetting = false;
        if (!IsScanModeEnabled)
        {
            _isActivated = false;
            return;
        }
        _sessionManager.AcquireSession(HandleBarcodeScanned);
    }

    /// <summary>
    /// Входит в режим сброса — блокирует ввод сканера до завершения сброса.
    /// </summary>
    public void EnterResettingMode()
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
