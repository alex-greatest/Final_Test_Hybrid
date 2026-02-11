using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private static readonly TimeSpan SettlementPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SettlementDiagnosticInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Логирует диагностический снимок при длительном ожидании settlement карты.
    /// </summary>
    private void LogSettlementWaitSnapshot(TimeSpan waitDuration)
    {
        var executorsSnapshot = string.Join(
            "; ",
            _executors.Select(executor =>
                $"ColumnIndex={executor.ColumnIndex}, IsFailed={executor.HasFailed}, CanSkip={executor.CanSkip}, Status={executor.Status ?? "-"}, FailedStep={executor.FailedStep?.Name ?? "-"}"));

        _logger.LogWarning(
            "Ожидание settlement продолжается: WaitSeconds={WaitSeconds}, CurrentMapIndex={CurrentMapIndex}, State={State}, HasPendingErrors={HasPendingErrors}, ErrorDrainCompleted={ErrorDrainCompleted}, RetryActive={RetryActive}, HasPendingRetries={HasPendingRetries}, CurrentInterrupt={CurrentInterrupt}, Executors=[{Executors}]",
            (int)waitDuration.TotalSeconds,
            CurrentMapIndex,
            StateManager.State,
            StateManager.HasPendingErrors,
            _errorDrainTask == null || _errorDrainTask.IsCompleted,
            _retryState.IsActive,
            HasPendingRetries(),
            _errorCoordinator.CurrentInterrupt,
            executorsSnapshot);
    }
}
