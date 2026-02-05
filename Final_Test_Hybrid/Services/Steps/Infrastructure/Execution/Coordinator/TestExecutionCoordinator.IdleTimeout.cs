using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private static readonly TimeSpan ExecutorsIdlePollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan ExecutorsIdleTimeout = TimeSpan.FromSeconds(60);

    private bool ShouldFreezeIdleTimeout()
    {
        return _errorCoordinator.CurrentInterrupt == InterruptReason.AutoModeDisabled
            || (StateManager.State == ExecutionState.PausedOnError && StateManager.HasPendingErrors);
    }

    /// <summary>
    /// Обрабатывает таймаут ожидания idle исполнителей как жёсткую остановку теста.
    /// </summary>
    private int ThrowExecutorsIdleTimeout()
    {
        _logger.LogError("Таймаут ожидания idle исполнителей");
        _testLogger.LogError(null, "Ошибка: таймаут ожидания idle исполнителей");
        LogExecutorsIdleTimeoutSnapshot();
        Stop(ExecutionStopReason.Operator, "Таймаут ожидания idle исполнителей", markFailed: true);
        throw new OperationCanceledException("Executors idle timeout", GetCancellationToken());
    }

    private void LogExecutorsIdleTimeoutSnapshot()
    {
        var executorsSnapshot = string.Join(
            "; ",
            _executors.Select(executor =>
                $"ColumnIndex={executor.ColumnIndex}, IsVisible={executor.IsVisible}, IsFailed={executor.HasFailed}, CanRetry={executor.FailedStep != null}, CanSkip={executor.CanSkip}"));

        _logger.LogError(
            "Диагностика таймаута ожидания idle исполнителей: CurrentMapIndex={CurrentMapIndex}, State={State}, HasPendingErrors={HasPendingErrors}, RetryActive={RetryActive}, HasPendingRetries={HasPendingRetries}, CurrentInterrupt={CurrentInterrupt}, Executors=[{Executors}]",
            CurrentMapIndex,
            StateManager.State,
            StateManager.HasPendingErrors,
            _retryState.IsActive,
            HasPendingRetries(),
            _errorCoordinator.CurrentInterrupt,
            executorsSnapshot);
    }
}
