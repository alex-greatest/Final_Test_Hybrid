using Final_Test_Hybrid.Models.Steps;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;

/// <summary>
/// Handles step errors by listening to PLC signals and resolving errors via Retry or Skip.
/// Subscribed to ErrorPlcMonitor at construction time (singleton lifecycle).
/// </summary>
public class StepErrorHandler
{
    private readonly ExecutionStateManager _stateManager;
    private readonly ILogger<StepErrorHandler> _logger;

    public event Action<ErrorResolution>? OnResolutionReceived;

    public StepErrorHandler(
        ExecutionStateManager stateManager,
        ErrorPlcMonitor plcMonitor,
        ILogger<StepErrorHandler> logger)
    {
        _stateManager = stateManager;
        _logger = logger;
        plcMonitor.OnSignalsChanged += HandlePlcSignals;
    }

    private void HandlePlcSignals(bool retry, bool skip)
    {
        if (!_stateManager.CanProcessSignals)
        {
            return;
        }
        if (retry)
        {
            ProcessRetrySignal();
            return;
        }
        if (skip)
        {
            ProcessSkipSignal();
        }
    }

    private void ProcessRetrySignal()
    {
        _logger.LogInformation("Retry signal received");
        OnResolutionReceived?.Invoke(ErrorResolution.Retry);
        _stateManager.TransitionTo(ExecutionState.Running);
    }

    private void ProcessSkipSignal()
    {
        _logger.LogInformation("Skip signal received");
        OnResolutionReceived?.Invoke(ErrorResolution.Skip);
        _stateManager.TransitionTo(ExecutionState.Running);
    }
}
