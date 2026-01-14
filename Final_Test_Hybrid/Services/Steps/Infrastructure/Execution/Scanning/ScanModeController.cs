using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Управляет режимом сканирования: активация/деактивация на основе состояния оператора и автомата.
/// Координирует регистрацию в MessageService и управление сессией сканера.
/// </summary>
public class ScanModeController : IDisposable
{
    private const int MessagePriority = 100;
    private const string ScanPromptMessage = "Отсканируйте серийный номер котла";
    private readonly ScanStateManager _scanStateManager;
    private readonly ScanSessionManager _sessionManager;
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly MessageService _messageService;
    private readonly ExecutionMessageState _executionMessageState;
    private readonly StepStatusReporter _statusReporter;
    private readonly PreExecutionCoordinator _preExecutionCoordinator;
    private CancellationTokenSource? _loopCts;
    private object? _messageProviderKey;
    private bool _disposed;
    public bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    public ScanModeController(
        ScanStateManager scanStateManager,
        ScanSessionManager sessionManager,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        MessageService messageService,
        ExecutionMessageState executionMessageState,
        StepStatusReporter statusReporter,
        PreExecutionCoordinator preExecutionCoordinator)
    {
        _scanStateManager = scanStateManager;
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _messageService = messageService;
        _executionMessageState = executionMessageState;
        _statusReporter = statusReporter;
        _preExecutionCoordinator = preExecutionCoordinator;
        SubscribeToEvents();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        _messageProviderKey = _messageService.RegisterProvider(MessagePriority, GetScanMessage);
        UpdateScanModeState();
    }

    private string? GetScanMessage()
    {
        return IsScanModeEnabled ? ScanPromptMessage : null;
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
    }

    private void TryActivateScanMode()
    {
        if (_scanStateManager.State != ScanState.Disabled)
        {
            return;
        }
        _scanStateManager.TryTransitionTo(ScanState.Ready, () =>
        {
            _sessionManager.AcquireSession(HandleBarcodeScanned);
            AddScanStepToGrid();
            StartMainLoop();
        });
    }

    private void AddScanStepToGrid()
    {
        if (_scanStateManager.ActiveScanStepId.HasValue)
        {
            return;
        }
        var scanStep = _stepRegistry.GetOrderedSteps().FirstOrDefault();
        if (scanStep == null)
        {
            return;
        }
        var stepId = _statusReporter.ReportStepStarted(scanStep);
        _scanStateManager.SetActiveScanStepId(stepId);
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
        if (_scanStateManager.State == ScanState.Resetting)
        {
            return;
        }
        _scanStateManager.TryTransitionTo(ScanState.Disabled, () =>
        {
            _sessionManager.ReleaseSession();
            if (!_operatorState.IsAuthenticated)
            {
                _statusReporter.ClearAll();
                _scanStateManager.SetActiveScanStepId(null);
            }
        });
    }

    public void TransitionToReady()
    {
        if (!IsScanModeEnabled)
        {
            _scanStateManager.TryTransitionTo(ScanState.Disabled);
            return;
        }
        _scanStateManager.TryTransitionTo(ScanState.Ready, () =>
        {
            _executionMessageState.Clear();
            _sessionManager.AcquireSession(HandleBarcodeScanned);
        });
    }

    /// <summary>
    /// Входит в режим сброса — блокирует ввод сканера до завершения сброса.
    /// </summary>
    public void EnterResettingMode()
    {
        _scanStateManager.TryTransitionTo(ScanState.Resetting, () =>
        {
            _sessionManager.ReleaseSession();
        });
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
        if (_messageProviderKey != null)
        {
            _messageService.UnregisterProvider(_messageProviderKey);
        }
    }
}
