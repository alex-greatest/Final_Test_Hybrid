namespace Final_Test_Hybrid.Services.Main.Messages;

using Models;
using OpcUa.Connection;
using SpringBoot.Operator;
using Steps.Infrastructure.Execution;
using Steps.Infrastructure.Execution.ErrorCoordinator;
using Steps.Infrastructure.Execution.PreExecution;
using Steps.Infrastructure.Execution.Scanning;
using PlcReset;

public class MessageService
{
    private readonly Lock _lock = new();

    private readonly OperatorState _operator;
    private readonly AutoReadySubscription _autoReady;
    private readonly OpcUaConnectionState _connection;
    private readonly ScanModeController _scanMode;
    private readonly ExecutionPhaseState _phaseState;
    private readonly ErrorCoordinator _errorCoord;
    private readonly PlcResetCoordinator _resetCoord;
    private readonly PreExecutionCoordinator _preExecutionCoord;
    private readonly RuntimeTerminalState _runtimeTerminalState;
    private readonly BoilerState _boilerState;

    public event Action? OnChange;

    public MessageService(
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        OpcUaConnectionState connection,
        ScanModeController scanMode,
        ExecutionPhaseState phaseState,
        ErrorCoordinator errorCoord,
        PlcResetCoordinator resetCoord,
        PreExecutionCoordinator preExecutionCoord,
        RuntimeTerminalState runtimeTerminalState,
        BoilerState boilerState)
    {
        _operator = operatorState;
        _autoReady = autoReady;
        _connection = connection;
        _scanMode = scanMode;
        _phaseState = phaseState;
        _errorCoord = errorCoord;
        _resetCoord = resetCoord;
        _preExecutionCoord = preExecutionCoord;
        _runtimeTerminalState = runtimeTerminalState;
        _boilerState = boilerState;
        SubscribeToChanges();
    }

    private bool IsResetUiBusy()
    {
        return _resetCoord.IsActive || _preExecutionCoord.IsPostAskEndFlowActive();
    }

    private MessageSnapshot CaptureSnapshot()
    {
        return new MessageSnapshot(
            _operator.IsAuthenticated,
            _autoReady.IsReady,
            _connection.IsConnected,
            _scanMode.IsScanModeEnabled,
            _boilerState.IsTestRunning,
            _phaseState.Phase,
            _errorCoord.CurrentInterrupt,
            IsResetUiBusy(),
            _runtimeTerminalState.IsCompletionActive,
            _runtimeTerminalState.IsPostAskEndActive);
    }

    private void SubscribeToChanges()
    {
        _operator.OnStateChanged += NotifyChanged;
        _autoReady.OnStateChanged += NotifyChanged;
        _connection.ConnectionStateChanged += _ => NotifyChanged();
        _scanMode.OnStateChanged += NotifyChanged;
        _phaseState.OnChanged += NotifyChanged;
        _errorCoord.OnInterruptChanged += NotifyChanged;
        _resetCoord.OnActiveChanged += NotifyChanged;
        _preExecutionCoord.OnStateChanged += NotifyChanged;
        _runtimeTerminalState.OnChanged += NotifyChanged;
        _boilerState.OnChanged += NotifyChanged;
    }

    public string CurrentMessage
    {
        get
        {
            lock (_lock)
            {
                var snapshot = CaptureSnapshot();
                return MessageServiceResolver.Resolve(snapshot);
            }
        }
    }

    private void NotifyChanged() => OnChange?.Invoke();
}
