using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    // === Состояние подписок ===
    private TaskCompletionSource<PreExecutionResolution>? _externalSignal;
    private bool _subscribed;
    private int _interruptDialogActive;
    private CancellationTokenSource? _interruptDialogCts;

    #region Subscriptions

    private void EnsureSubscribed()
    {
        if (_subscribed)
        {
            return;
        }
        _subscribed = true;
        SubscribeToStopSignals();
        ReplayPendingAutoReadyRequestIfAny();
    }

    private void SubscribeToStopSignals()
    {
        coordinators.PlcResetCoordinator.OnForceStop += HandleSoftStop;
        coordinators.PlcResetCoordinator.OnAskEndReceived += HandleGridClear;
        coordinators.ErrorCoordinator.OnReset += HandleHardReset;
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
        state.FlowState.RequestStop(stopReason, stopAsFailure: true);
        SignalReset(exitReason);
        StopChangeoverTimerForReset(GetChangeoverResetMode());

        if (TryCancelActiveOperation(exitReason))
        {
            // Очистка произойдёт в HandleCycleExit
        }
        else
        {
            // Нет активной операции — очищаем сразу
            HandleCycleExit(exitReason);
        }
        SignalResolution(resolution);
    }

    private bool TryCancelActiveOperation(CycleExitReason exitReason)
    {
        if (coordinators.TestCoordinator.IsRunning || state.ActivityTracker.IsPreExecutionActive)
        {
            _pendingExitReason = exitReason;
            _currentCts?.Cancel();
            return true;
        }
        return false;
    }

    private async void HandleGridClear()
    {
        try
        {
            await ExecuteGridClearAsync();
        }
        catch (Exception ex)
        {
            infra.Logger.LogError(ex, "Ошибка в HandleGridClear");
            ResetInterruptState();
            TryCompletePlcReset();
        }
    }

    private async Task ExecuteGridClearAsync()
    {
        var window = Volatile.Read(ref _currentAskEndWindow);
        var resetSequence = GetResetSequenceSnapshot();
        if (window == null)
        {
            LogAskEndIgnoredAsStale(0, resetSequence);
            LogInterruptDialogSuppressed("stale_seq", resetSequence, Volatile.Read(ref _interruptDialogAllowedSequence));
            return;
        }
        if (window.Sequence != resetSequence)
        {
            LogAskEndIgnoredAsStale(window.Sequence, resetSequence);
            LogInterruptDialogSuppressed("stale_seq", resetSequence, Volatile.Read(ref _interruptDialogAllowedSequence));
            return;
        }
        RecordAskEndSequence(window.Sequence);
        if (!TryRunResetCleanupOnce())
        {
            infra.Logger.LogDebug(
                "Пропуск reset-cleanup: path={CleanupPath}, reason={SkipReason}, seq={ResetSequence}",
                "AskEnd",
                "already_done",
                resetSequence);
            CompletePlcResetOrLogStale(window.Sequence);
            return;
        }
        if (GetResetSequenceSnapshot() != window.Sequence)
        {
            Interlocked.Exchange(ref _resetCleanupDone, 0);
            LogAskEndIgnoredAsStale(window.Sequence, GetResetSequenceSnapshot());
            CompletePlcResetOrLogStale(window.Sequence);
            return;
        }
        infra.Logger.LogDebug(
            "Выполнение reset-cleanup: path={CleanupPath}, seq={ResetSequence}",
            "AskEnd",
            resetSequence);

        var context = CaptureAndClearState();
        if (!ShouldShowInterruptDialog(context))
        {
            LogInterruptDialogSuppressed("conditions_not_met", window.Sequence, Volatile.Read(ref _interruptDialogAllowedSequence));
            CompletePlcResetOrLogStale(window.Sequence);
            return;
        }
        if (Volatile.Read(ref _interruptDialogAllowedSequence) != window.Sequence)
        {
            LogInterruptDialogSuppressed("series_latch", window.Sequence, Volatile.Read(ref _interruptDialogAllowedSequence));
            CompletePlcResetOrLogStale(window.Sequence);
            return;
        }
        await TryShowInterruptDialogAsync(context.SerialNumber!, window.Sequence);
        CompletePlcResetOrLogStale(window.Sequence);
    }

    private (bool WasTestRunning, string? SerialNumber) CaptureAndClearState()
    {
        var wasTestRunning = state.BoilerState.IsTestRunning;
        var serialNumber = state.BoilerState.SerialNumber;

        ClearStateOnReset();
        infra.StatusReporter.ClearAllExceptScan();

        return (wasTestRunning, serialNumber);
    }

    private bool ShouldShowInterruptDialog((bool WasTestRunning, string? SerialNumber) context)
    {
        return context.WasTestRunning
            && context.SerialNumber != null
            && infra.AppSettings.UseInterruptReason;
    }

    private async Task TryShowInterruptDialogAsync(string serialNumber, int resetSequence)
    {
        if (!TryAcquireDialogLock())
        {
            CancelActiveDialog();
            return;
        }
        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _interruptDialogCts, cts);
        DisposeOldCts(oldCts);
        MarkInterruptDialogActive(resetSequence);

        try
        {
            await ShowInterruptReasonDialogAsync(serialNumber, cts.Token);
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

    private async Task ShowInterruptReasonDialogAsync(string serialNumber, CancellationToken ct)
    {
        void HandleCancel() => CancelActiveDialog();
        coordinators.PlcResetCoordinator.OnForceStop += HandleCancel;
        coordinators.ErrorCoordinator.OnReset += HandleCancel;

        try
        {
            var useMes = infra.AppSettings.UseMes;
            // Временный компромисс: для soft reset сразу показываем ввод причины без окна
            // авторизации администратора. Чтобы вернуть старое поведение (Auth -> Reason),
            // достаточно поставить false.
            const bool bypassAdminAuthInSoftResetInterrupt = true;
            var requireAdminAuth = useMes && !bypassAdminAuthInSoftResetInterrupt;
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
            HandleChangeoverAfterInterrupt(result);
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
        var resetSequence = BeginPlcReset();
        var isFirstSoftResetInSeries = Interlocked.CompareExchange(
            ref _interruptReasonUsedInCurrentResetSeries,
            1,
            0) == 0;
        var allowedSequence = isFirstSoftResetInSeries ? resetSequence : 0;
        Volatile.Write(ref _interruptDialogAllowedSequence, allowedSequence);
        LogInterruptReasonSeriesLatchSet(resetSequence, isFirstSoftResetInSeries);
        HandleStopSignal(PreExecutionResolution.SoftStop);
    }

    private void HandleHardReset()
    {
        CancelActiveDialog();
        TryCompletePlcReset();
        // Атомарно читаем и сбрасываем - определяем источник
        var isPending = Interlocked.Exchange(ref coordinators.PlcResetCoordinator.PlcHardResetPending, 0);
        var origin = isPending == 1 ? ResetOriginPlc : ResetOriginNonPlc;
        Volatile.Write(ref _lastHardResetOrigin, origin);
        if (origin == ResetOriginNonPlc)
        {
            BeginResetCycle(ResetOriginNonPlc, ensureAskEndWindow: false);
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

    private void LogInterruptReasonSeriesLatchSet(int resetSequence, bool isFirst)
    {
        infra.Logger.LogInformation(
            "InterruptReasonSeriesLatchSet: seq={ResetSequence}, isFirst={IsFirst}",
            resetSequence,
            isFirst);
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
