namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Методы определения причины выхода из цикла.
/// </summary>
public partial class PreExecutionCoordinator
{
    /// <summary>
    /// Проверяет наличие причины остановки из любого источника.
    /// </summary>
    private bool TryGetStopExitReason(
        out CycleExitReason reason,
        TaskCompletionSource<CycleExitReason>? resetSignalSnapshot = null)
    {
        if (TryGetPendingExitReason(out reason))
        {
            return true;
        }

        if (TryGetResetSignalExitReason(resetSignalSnapshot ?? Volatile.Read(ref _resetSignal), out reason))
        {
            return true;
        }

        // ВАЖНО: единый snapshot для защиты от гонок
        var stopReason = state.FlowState.StopReason;
        return TryGetForceStopExitReason(stopReason, out reason) || TryGetMappedStopExitReason(stopReason, out reason);
    }

    private CycleExitReason ResolveStopExitReasonOrFallback(
        CycleExitReason fallback,
        TaskCompletionSource<CycleExitReason>? resetSignalSnapshot = null)
    {
        return TryGetStopExitReason(out var reason, resetSignalSnapshot) ? reason : fallback;
    }

    /// <summary>
    /// Проверяет отложенную причину выхода (_pendingExitReason).
    /// </summary>
    private bool TryGetPendingExitReason(out CycleExitReason reason)
    {
        var pendingExitReason = Volatile.Read(ref _pendingExitReason);
        if (pendingExitReason < 0)
        {
            reason = default;
            return false;
        }

        reason = (CycleExitReason)pendingExitReason;
        return true;
    }

    /// <summary>
    /// Проверяет сигнал сброса (_resetSignal).
    /// </summary>
    private static bool TryGetResetSignalExitReason(
        TaskCompletionSource<CycleExitReason>? resetSignal,
        out CycleExitReason reason)
    {
        if (resetSignal?.Task.IsCompletedSuccessfully != true)
        {
            reason = default;
            return false;
        }
        reason = resetSignal.Task.Result;
        return true;
    }

    private void SetPendingExitReason(CycleExitReason reason)
    {
        Volatile.Write(ref _pendingExitReason, (int)reason);
    }

    private void ClearPendingExitReason()
    {
        Volatile.Write(ref _pendingExitReason, -1);
    }

    /// <summary>
    /// Проверяет принудительную остановку от PLC (PlcForceStop).
    /// </summary>
    private bool TryGetForceStopExitReason(ExecutionStopReason stopReason, out CycleExitReason reason)
    {
        if (stopReason != ExecutionStopReason.PlcForceStop)
        {
            reason = default;
            return false;
        }
        reason = state.BoilerState.IsTestRunning
            ? CycleExitReason.HardReset
            : CycleExitReason.SoftReset;
        return true;
    }

    /// <summary>
    /// Проверяет маппинг StopReason → CycleExitReason.
    /// </summary>
    private bool TryGetMappedStopExitReason(ExecutionStopReason stopReason, out CycleExitReason reason)
    {
        var mapped = MapStopReasonToExitReason(stopReason);
        if (!mapped.HasValue)
        {
            reason = default;
            return false;
        }
        reason = mapped.Value;
        return true;
    }

    /// <summary>
    /// Маппинг причины остановки в причину выхода из цикла.
    /// </summary>
    private static CycleExitReason? MapStopReasonToExitReason(ExecutionStopReason stopReason)
    {
        return stopReason switch
        {
            ExecutionStopReason.PlcSoftReset => CycleExitReason.SoftReset,
            ExecutionStopReason.PlcHardReset => CycleExitReason.HardReset,
            _ => null
        };
    }
}
