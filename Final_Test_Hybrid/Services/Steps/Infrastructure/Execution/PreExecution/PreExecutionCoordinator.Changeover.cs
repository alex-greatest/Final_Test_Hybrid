using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    // === Состояние Changeover ===
    private const int ChangeoverStartNone = 0;
    private const int ChangeoverStartPending = 1;
    private const int ChangeoverTriggerNone = 0;
    private const int ChangeoverTriggerAskEndOnly = 1;
    private const int ChangeoverTriggerReasonSaved = 2;
    private const int ChangeoverTriggerSecondReset = 3;
    private int _changeoverStartState;
    private int _changeoverTrigger;
    private int _changeoverPendingSequence;
    private int _changeoverAskEndSequence;
    private int _changeoverReasonSaved;
    private int _changeoverStartedSequence = -1;

    private enum ChangeoverResetMode
    {
        Immediate,
        WaitForReason,
        WaitForAskEndOnly
    }

    private bool ShouldDelayChangeoverStart()
    {
        var serialNumber = state.BoilerState.SerialNumber;
        var stopReason = state.FlowState.StopReason;
        return stopReason is ExecutionStopReason.PlcSoftReset or ExecutionStopReason.PlcHardReset or ExecutionStopReason.PlcForceStop
            && state.BoilerState.IsTestRunning
            && serialNumber != null
            && infra.AppSettings.UseInterruptReason;
    }

    private bool ShouldDelayChangeoverUntilAskEndOnly()
    {
        var stopReason = state.FlowState.StopReason;
        return stopReason is ExecutionStopReason.PlcSoftReset or ExecutionStopReason.PlcHardReset or ExecutionStopReason.PlcForceStop;
    }

    private ChangeoverResetMode GetChangeoverResetMode()
    {
        return ShouldDelayChangeoverStart()
            ? ChangeoverResetMode.WaitForReason
            : ShouldDelayChangeoverUntilAskEndOnly()
                ? ChangeoverResetMode.WaitForAskEndOnly
                : ChangeoverResetMode.Immediate;
    }

    private int GetResetSequenceSnapshot()
    {
        return Volatile.Read(ref _resetSequence);
    }

    internal int GetCurrentResetSequenceSnapshot()
    {
        return GetResetSequenceSnapshot();
    }

    private void ArmChangeoverPendingForReset(int resetSequence, int defaultTrigger)
    {
        var startState = Interlocked.CompareExchange(ref _changeoverStartState, ChangeoverStartPending, ChangeoverStartNone);
        var existingSequence = Volatile.Read(ref _changeoverPendingSequence);
        var shouldUpdate = startState != ChangeoverStartPending || existingSequence != resetSequence;
        if (!shouldUpdate)
        {
            return;
        }
        var isSecondReset = startState == ChangeoverStartPending;
        UpdateChangeoverPendingState(isSecondReset, resetSequence, defaultTrigger);
        TryStartChangeoverAfterAskEnd();
    }

    private void UpdateChangeoverPendingState(bool isSecondReset, int resetSequence, int defaultTrigger)
    {
        var trigger = isSecondReset ? ChangeoverTriggerSecondReset : defaultTrigger;
        Volatile.Write(ref _changeoverTrigger, trigger);
        Volatile.Write(ref _changeoverPendingSequence, resetSequence);
        Volatile.Write(ref _changeoverReasonSaved, 0);
    }

    private void RecordAskEndSequence(int resetSequence)
    {
        Volatile.Write(ref _changeoverAskEndSequence, resetSequence);
        TryStartChangeoverAfterAskEnd();
    }

    private void MarkChangeoverReasonSaved()
    {
        Volatile.Write(ref _changeoverReasonSaved, 1);
        TryStartChangeoverAfterAskEnd();
    }

    private void TryStartChangeoverAfterAskEnd()
    {
        if (!IsChangeoverStartReady())
        {
            return;
        }
        StartChangeoverTimerFromPending();
    }

    private bool IsChangeoverStartReady()
    {
        return IsChangeoverPendingAndAskEndMatched() && IsChangeoverTriggerSatisfied();
    }

    private bool IsChangeoverPendingAndAskEndMatched()
    {
        var startState = Volatile.Read(ref _changeoverStartState);
        if (startState != ChangeoverStartPending)
        {
            return false;
        }
        return Volatile.Read(ref _changeoverPendingSequence) == Volatile.Read(ref _changeoverAskEndSequence);
    }

    private bool IsChangeoverTriggerSatisfied()
    {
        var trigger = Volatile.Read(ref _changeoverTrigger);
        return trigger switch
        {
            ChangeoverTriggerAskEndOnly => true,
            ChangeoverTriggerSecondReset => true,
            ChangeoverTriggerReasonSaved => Volatile.Read(ref _changeoverReasonSaved) == 1,
            _ => false
        };
    }

    private void StartChangeoverTimerFromPending()
    {
        var pendingSequence = Volatile.Read(ref _changeoverPendingSequence);
        var currentSequence = GetResetSequenceSnapshot();
        if (pendingSequence != currentSequence)
        {
            LogChangeoverStartRejectedAsStale(
                currentSequence,
                Volatile.Read(ref _changeoverAskEndSequence),
                pendingSequence);
            ClearChangeoverPendingState();
            return;
        }
        if (Interlocked.CompareExchange(
            ref _changeoverStartState,
            ChangeoverStartNone,
            ChangeoverStartPending) != ChangeoverStartPending)
        {
            return;
        }
        ClearChangeoverPendingState();
        StartChangeoverTimerImmediate();
    }

    private void ClearChangeoverPendingState()
    {
        Volatile.Write(ref _changeoverStartState, ChangeoverStartNone);
        Volatile.Write(ref _changeoverTrigger, ChangeoverTriggerNone);
        Volatile.Write(ref _changeoverPendingSequence, 0);
        Volatile.Write(ref _changeoverReasonSaved, 0);
    }

    private void StartChangeoverTimerForImmediateReset()
    {
        ClearChangeoverPendingState();
        StartChangeoverTimerImmediate();
    }

    private void StartChangeoverTimerImmediate()
    {
        var currentSeq = GetResetSequenceSnapshot();
        if (currentSeq == Volatile.Read(ref _changeoverStartedSequence))
        {
            LogChangeoverStartSkippedDuplicateSeq(currentSeq);
            return;
        }
        var askEndSequence = Volatile.Read(ref _changeoverAskEndSequence);
        var stopReason = state.FlowState.StopReason;
        if (stopReason is ExecutionStopReason.PlcSoftReset or ExecutionStopReason.PlcHardReset or ExecutionStopReason.PlcForceStop
            && askEndSequence != currentSeq)
        {
            infra.Logger.LogInformation(
                "ChangeoverStartDeferredBySeq: seq={ResetSequence}, askEndSeq={AskEndSequence}",
                currentSeq,
                askEndSequence);
            var trigger = ShouldDelayChangeoverStart()
                ? ChangeoverTriggerReasonSaved
                : ChangeoverTriggerAskEndOnly;
            TryArmChangeoverPending(trigger);
            return;
        }
        Volatile.Write(ref _changeoverStartedSequence, currentSeq);
        state.BoilerState.ResetAndStartChangeoverTimer();
    }

    private void HandleChangeoverAfterInterrupt(InterruptFlowResult result)
    {
        if (result.IsSuccess)
        {
            MarkChangeoverReasonSaved();
        }
    }

    private void HandleChangeoverAfterReset(ChangeoverResetMode mode)
    {
        switch (mode)
        {
            case ChangeoverResetMode.Immediate:
                // Immediate стартует сразу (PLC reset будет gated в StartChangeoverTimerImmediate)
                StartChangeoverTimerForImmediateReset();
                break;
            case ChangeoverResetMode.WaitForReason:
                TryArmChangeoverPending(ChangeoverTriggerReasonSaved);
                break;
            case ChangeoverResetMode.WaitForAskEndOnly:
                TryArmChangeoverPending(ChangeoverTriggerAskEndOnly);
                break;
            default:
                infra.Logger.LogWarning("Неизвестный режим старта changeover после reset: {Mode}", mode);
                break;
        }
    }

    /// <summary>
    /// Guard: не перезапускать pending для того же seq.
    /// Dedup привязан к resetSequence; каждый новый reset-cycle обязан заранее увеличить _resetSequence.
    /// </summary>
    private void TryArmChangeoverPending(int trigger)
    {
        var resetSequence = GetResetSequenceSnapshot();
        if (resetSequence == Volatile.Read(ref _changeoverStartedSequence))
        {
            return;
        }
        ArmChangeoverPendingForReset(resetSequence, trigger);
    }

    private void StopChangeoverTimerForReset(ChangeoverResetMode mode)
    {
        if (mode == ChangeoverResetMode.Immediate)
        {
            return;
        }
        StopChangeoverAndAllowRestart();
    }

    /// <summary>
    /// Останавливает таймер и разрешает перезапуск (сбрасывает seq).
    /// </summary>
    private void StopChangeoverAndAllowRestart()
    {
        state.BoilerState.StopChangeoverTimer();
        Volatile.Write(ref _changeoverStartedSequence, -1);
    }

    /// <summary>
    /// Для не-PLC reset отправляет синтетические сигналы changeover.
    /// </summary>
    private void TrySendSyntheticChangeoverSignals(ChangeoverResetMode mode)
    {
        switch (Volatile.Read(ref _lastHardResetOrigin))
        {
            case ResetOriginNonPlc:
                SendSyntheticChangeoverSignals(mode);
                break;
        }
    }

    private void SendSyntheticChangeoverSignals(ChangeoverResetMode mode)
    {
        var resetSequence = GetResetSequenceSnapshot();
        switch (mode)
        {
            case ChangeoverResetMode.Immediate:
                break;
            case ChangeoverResetMode.WaitForReason:
                MarkChangeoverReasonSaved();
                RecordAskEndSequence(resetSequence);
                break;
            case ChangeoverResetMode.WaitForAskEndOnly:
                RecordAskEndSequence(resetSequence);
                break;
            default:
                infra.Logger.LogWarning("Неизвестный режим synthetic changeover-сигналов: {Mode}", mode);
                break;
        }
    }

    private void LogChangeoverStartSkippedDuplicateSeq(int currentSequence)
    {
        infra.Logger.LogInformation(
            "ChangeoverStartSkippedDuplicateSeq: seq={CurrentSequence}",
            currentSequence);
    }

    private void LogChangeoverStartRejectedAsStale(int currentSequence, int askEndSequence, int pendingSequence)
    {
        infra.Logger.LogInformation(
            "ChangeoverStartRejectedAsStale: seq={CurrentSequence}, askEndSeq={AskEndSequence}, pendingSeq={PendingSequence}",
            currentSequence,
            askEndSequence,
            pendingSequence);
    }
}
