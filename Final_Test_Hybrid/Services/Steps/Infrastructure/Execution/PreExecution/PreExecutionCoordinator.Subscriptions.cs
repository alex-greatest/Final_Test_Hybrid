using System.Diagnostics.CodeAnalysis;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    // === Состояние подписок ===
    private TaskCompletionSource<PreExecutionResolution>? _externalSignal;
    private bool _resetSubscribed;
    private bool _autoReadySubscribed;
    private int _interruptDialogActive;
    private CancellationTokenSource? _interruptDialogCts;

    #region Subscriptions

    internal void EnsureResetSignalsSubscribed()
    {
        if (_resetSubscribed)
        {
            return;
        }

        _resetSubscribed = true;
        SubscribeToResetSignals();
    }

    private void EnsureSubscribed()
    {
        // Reset-path должен жить даже до старта main loop и логина оператора.
        EnsureResetSignalsSubscribed();

        // AutoReady replay оставляем lazy, чтобы не вернуть ранний старт changeover.
        if (_autoReadySubscribed)
        {
            return;
        }

        _autoReadySubscribed = true;
        SubscribeToAutoReadySignals();
        ReplayPendingAutoReadyRequestIfAny();
    }

    private void SubscribeToResetSignals()
    {
        coordinators.PlcResetCoordinator.OnForceStop += HandleSoftStop;
        coordinators.PlcResetCoordinator.OnAskEndReceived += HandleGridClear;
        coordinators.PlcResetCoordinator.OnResetCompleted += HandlePlcResetCompleted;
        coordinators.ErrorCoordinator.OnReset += HandleHardReset;
    }

    private void SubscribeToAutoReadySignals()
    {
        coordinators.ChangeoverStartGate.OnAutoReadyRequested += HandleAutoReadyRequested;
    }

    private void ReplayPendingAutoReadyRequestIfAny()
    {
        if (!coordinators.ChangeoverStartGate.TryConsumePendingAutoReadyRequest())
        {
            return;
        }
        infra.Logger.LogInformation("AutoReadyReplayConsumed");
        HandleAutoReadyRequested();
    }

    private void HandleStopSignal(PreExecutionResolution resolution)
    {
        infra.StepTimingService.PauseAllColumnsTiming();
        var exitReason = resolution == PreExecutionResolution.SoftStop
            ? CycleExitReason.SoftReset
            : CycleExitReason.HardReset;
        var stopReason = resolution == PreExecutionResolution.SoftStop
            ? ExecutionStopReason.PlcSoftReset
            : ExecutionStopReason.PlcHardReset;
        var changeoverMode = LatchChangeoverResetModeForCurrentReset();
        state.FlowState.RequestStop(stopReason, stopAsFailure: true);
        SignalReset(exitReason);
        StopChangeoverTimerForReset(changeoverMode);

        if (TryCancelActiveOperation(exitReason))
        {
            // Очистка произойдёт в HandleCycleExit
        }
        else
        {
            // Нет активной операции — обрабатываем выход сразу
            HandleCycleExit(exitReason);
        }
        SignalResolution(resolution);
    }

    private bool TryCancelActiveOperation(CycleExitReason exitReason)
    {
        if (coordinators.TestCoordinator.IsRunning || state.ActivityTracker.IsPreExecutionActive)
        {
            SetPendingExitReason(exitReason);
            _currentCts?.Cancel();
            return true;
        }
        return false;
    }

    private void HandleGridClear()
    {
        if (!TryBeginPostAskEndFlow(out var window))
        {
            return;
        }

        _ = ExecutePostAskEndFlowAsync(window);
    }

    private async Task ExecutePostAskEndFlowAsync(ResetAskEndWindow window)
    {
        try
        {
            await HandlePostAskEndDecisionAsync(window);
        }
        catch (OperationCanceledException)
        {
            infra.Logger.LogInformation("Post-AskEnd flow отменён новым reset");
        }
        catch (Exception ex)
        {
            infra.Logger.LogError(ex, "Ошибка в post-AskEnd flow: seq={ResetSequence}", window.Sequence);
            HandlePostAskEndFailure(window.Sequence);
        }
        finally
        {
            EnsurePostAskEndFlowReleased(window.Sequence);
        }
    }

    private void HandlePostAskEndFailure(int expectedSequence)
    {
        ResetInterruptState();
        try
        {
            FinalizeResetCleanup(expectedSequence);
        }
        catch (Exception ex)
        {
            infra.Logger.LogError(
                ex,
                "Fail-safe cleanup после ошибки post-AskEnd завершился с исключением: seq={ResetSequence}",
                expectedSequence);
        }
    }

    private bool TryBeginPostAskEndFlow([NotNullWhen(true)] out ResetAskEndWindow? window)
    {
        if (!TryGetCurrentAskEndWindow(out window))
        {
            return false;
        }

        // Поднимаем guard синхронно до первого await, чтобы ранний
        // OnResetCompleted не успел очистить состояние до post-AskEnd ветки.
        StartPostAskEndFlow();
        return true;
    }

    private void HandlePlcResetCompleted()
    {
        if (Volatile.Read(ref _postAskEndActive) == 1)
        {
            infra.Logger.LogDebug("Пропуск OnResetCompleted cleanup: post-AskEnd flow ещё активен");
            return;
        }

        var resetSequence = GetResetSequenceSnapshot();
        if (!TryRunResetCleanupOnce())
        {
            infra.Logger.LogDebug(
                "Пропуск reset-cleanup: path={CleanupPath}, reason={SkipReason}, seq={ResetSequence}",
                "OnResetCompleted",
                "already_done",
                resetSequence);
            return;
        }

        infra.Logger.LogDebug(
            "Выполнение reset-cleanup: path={CleanupPath}, seq={ResetSequence}",
            "OnResetCompleted",
            resetSequence);
        ClearStateOnReset();
        infra.StatusReporter.ClearAllExceptScan(SequenceClearMode.OperationalReset);
    }

    private async Task<InterruptFlowResult> TryShowInterruptDialogAsync(string serialNumber, int resetSequence, CancellationToken ct)
    {
        if (!TryAcquireDialogLock())
        {
            CancelActiveDialog();
            return InterruptFlowResult.Cancelled();
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var oldCts = Interlocked.Exchange(ref _interruptDialogCts, cts);
        DisposeOldCts(oldCts);
        MarkInterruptDialogActive(resetSequence);

        try
        {
            return await ShowInterruptReasonDialogAsync(serialNumber, cts.Token);
        }
        finally
        {
            ReleaseDialogLock(cts);
        }
    }

    private bool TryAcquireDialogLock()
    {
        return Interlocked.CompareExchange(ref _interruptDialogActive, 1, 0) == 0;
    }

    private static void DisposeOldCts(CancellationTokenSource? oldCts)
    {
        oldCts?.Dispose();
    }

    private void MarkInterruptDialogActive(int resetSequence)
    {
        Volatile.Write(ref _interruptReasonDialogSequence, resetSequence);
        infra.StepTimingService.PauseAllColumnsTiming();
        infra.Logger.LogInformation(
            "InterruptDialogTimingFreezeApplied: seq={ResetSequence}, scanAndColumnsPaused={TimersPaused}",
            resetSequence,
            true);
        OnStateChanged?.Invoke();
    }

    private void CancelActiveDialog()
    {
        var currentCts = Volatile.Read(ref _interruptDialogCts);
        SafeCancel(currentCts);
    }

    private void ReleaseDialogLock(CancellationTokenSource cts)
    {
        Volatile.Write(ref _interruptReasonDialogSequence, 0);
        Interlocked.Exchange(ref _interruptDialogActive, 0);
        Interlocked.CompareExchange(ref _interruptDialogCts, null, cts);
        OnStateChanged?.Invoke();
        cts.Dispose();
    }

    private void ResetInterruptState()
    {
        Volatile.Write(ref _interruptReasonDialogSequence, 0);
        Interlocked.Exchange(ref _interruptDialogActive, 0);
        OnStateChanged?.Invoke();
    }

    private static void SafeCancel(CancellationTokenSource? cts)
    {
        if (cts == null)
        {
            return;
        }
        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task<InterruptFlowResult> ShowInterruptReasonDialogAsync(string serialNumber, CancellationToken ct)
    {
        void HandleCancel() => CancelActiveDialog();
        coordinators.PlcResetCoordinator.OnForceStop += HandleCancel;
        coordinators.ErrorCoordinator.OnReset += HandleCancel;

        try
        {
            var useMes = infra.AppSettings.UseMes;
            var requireAdminAuth = useMes;
            var operatorUsername = state.OperatorState.Username ?? "Unknown";

            var result = await coordinators.DialogCoordinator.ShowInterruptReasonDialogAsync(
                serialNumber,
                (admin, reason, token) => infra.InterruptReasonRouter.SaveAsync(
                    serialNumber, admin, reason, useMes, token),
                useMes,
                requireAdminAuth,
                operatorUsername,
                ct);

            LogInterruptResult(result);
            return result;
        }
        finally
        {
            coordinators.PlcResetCoordinator.OnForceStop -= HandleCancel;
            coordinators.ErrorCoordinator.OnReset -= HandleCancel;
        }
    }

    private void LogInterruptResult(InterruptFlowResult result)
    {
        if (result.IsSuccess)
        {
            infra.Logger.LogInformation("Причина прерывания сохранена: {Admin}", result.AdminUsername);
        }
    }

    private void HandleSoftStop()
    {
        CancelActiveDialog();
        CancelPostAskEndFlow();

        var resetSequence = BeginPlcReset();

        // Пока пользователь не завершил окно причины через Save/Cancel,
        // новый soft reset снова разрешает показ окна на следующем AskEnd.
        var dialogCompletedInSeries =
            Volatile.Read(ref _interruptDialogCompletedInCurrentResetSeries) == 1;
        var allowedSequence = dialogCompletedInSeries ? 0 : resetSequence;
        Volatile.Write(ref _interruptDialogAllowedSequence, allowedSequence);

        LogInterruptDialogWindowArmed(resetSequence, dialogCompletedInSeries, allowedSequence);
        HandleStopSignal(PreExecutionResolution.SoftStop);
    }

    private void HandleHardReset()
    {
        CancelActiveDialog();
        CancelPostAskEndFlow();
        TryCompletePlcReset();
        // Атомарно читаем и сбрасываем - определяем источник
        var isPending = Interlocked.Exchange(ref coordinators.PlcResetCoordinator.PlcHardResetPending, 0);
        var origin = isPending == 1 ? ResetOriginPlc : ResetOriginNonPlc;
        Volatile.Write(ref _lastHardResetOrigin, origin);
        if (origin == ResetOriginNonPlc)
        {
            var barcodeWaitActive = HasActiveBarcodeWait();
            var resetSequence = BeginResetCycle(ResetOriginNonPlc, ensureAskEndWindow: false);
            infra.Logger.LogDebug(
                "non_plc_hard_reset_cancel_barcode_wait: origin={ResetSource}, seq={ResetSequence}, barcodeWaitActive={BarcodeWaitActive}",
                DescribeResetOrigin(origin),
                resetSequence,
                barcodeWaitActive);
        }
        HandleStopSignal(PreExecutionResolution.HardReset);
    }

    private void HandleAutoReadyRequested()
    {
        try
        {
            StartChangeoverTimerImmediate();
        }
        catch (Exception ex)
        {
            infra.Logger.LogError(ex, "Ошибка запуска changeover по сигналу AutoReady");
        }
    }

    private void CompletePlcResetOrLogStale(int expectedSequence)
    {
        if (CompletePlcReset(expectedSequence))
        {
            return;
        }
        LogAskEndIgnoredAsStale(expectedSequence, GetResetSequenceSnapshot());
    }

    private void LogInterruptDialogWindowArmed(
        int resetSequence,
        bool dialogCompletedInSeries,
        int allowedSequence)
    {
        infra.Logger.LogInformation(
            "InterruptReasonDialogWindowArmed: seq={ResetSequence}, completedInSeries={CompletedInSeries}, allowedSeq={AllowedSequence}",
            resetSequence,
            dialogCompletedInSeries,
            allowedSequence);
    }

    private void LogInterruptDialogSuppressed(string reason, int resetSequence, int allowedSequence)
    {
        infra.Logger.LogInformation(
            "InterruptReasonDialogSuppressed: reason={Reason}, seq={ResetSequence}, allowedSeq={AllowedSequence}",
            reason,
            resetSequence,
            allowedSequence);
    }

    private void LogAskEndIgnoredAsStale(int windowSequence, int currentSequence)
    {
        infra.Logger.LogInformation(
            "AskEndIgnoredAsStale: windowSeq={WindowSequence}, currentSeq={CurrentSequence}",
            windowSequence,
            currentSequence);
    }

    internal bool IsInterruptReasonDialogActive()
    {
        return Volatile.Read(ref _interruptDialogActive) == 1;
    }

    internal int GetInterruptReasonDialogSequenceSnapshot()
    {
        return Volatile.Read(ref _interruptReasonDialogSequence);
    }

    private void SignalResolution(PreExecutionResolution resolution)
    {
        infra.ErrorService.ClearActiveApplicationErrors();
        _externalSignal?.TrySetResult(resolution);
    }

    #endregion
}
