using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

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
    private readonly ITestStepLogger _testStepLogger;
    private readonly StepStatusReporter _statusReporter;
    private readonly IPreExecutionStepRegistry _stepRegistry;
    private Action<string>? _barcodeHandler;
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
        ITestStepLogger testStepLogger,
        StepStatusReporter statusReporter,
        IPreExecutionStepRegistry stepRegistry)
    {
        _scanStateManager = scanStateManager;
        _sessionManager = sessionManager;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _messageService = messageService;
        _executionMessageState = executionMessageState;
        _testStepLogger = testStepLogger;
        _statusReporter = statusReporter;
        _stepRegistry = stepRegistry;
        SubscribeToEvents();
    }

    public void Initialize(Action<string> barcodeHandler)
    {
        _barcodeHandler = barcodeHandler;
        UpdateScanModeState();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnStateChanged += UpdateScanModeState;
        _autoReady.OnStateChanged += UpdateScanModeState;
        _messageProviderKey = _messageService.RegisterProvider(MessagePriority, GetScanMessage);
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
        if (_scanStateManager.State != ScanState.Disabled || _barcodeHandler == null)
        {
            return;
        }
        _scanStateManager.TryTransitionTo(ScanState.Ready, () =>
        {
            _sessionManager.AcquireSession(_barcodeHandler);
            _testStepLogger.StartNewSession();
            AddScanStepToGrid();
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

    private void TryDeactivateScanMode()
    {
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
        if (!IsScanModeEnabled || _barcodeHandler == null)
        {
            _scanStateManager.TryTransitionTo(ScanState.Disabled);
            return;
        }
        _scanStateManager.TryTransitionTo(ScanState.Ready, () =>
        {
            _executionMessageState.Clear();
            _sessionManager.AcquireSession(_barcodeHandler);
        });
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _operatorState.OnStateChanged -= UpdateScanModeState;
        _autoReady.OnStateChanged -= UpdateScanModeState;
        if (_messageProviderKey != null)
        {
            _messageService.UnregisterProvider(_messageProviderKey);
        }
    }
}
