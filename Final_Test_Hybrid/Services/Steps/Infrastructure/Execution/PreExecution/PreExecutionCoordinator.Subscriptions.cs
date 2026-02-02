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
    }

    private void SubscribeToStopSignals()
    {
        coordinators.PlcResetCoordinator.OnForceStop += HandleSoftStop;
        coordinators.PlcResetCoordinator.OnAskEndReceived += HandleGridClear;
        coordinators.ErrorCoordinator.OnReset += HandleHardReset;
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
            CompletePlcReset();
        }
    }

    private async Task ExecuteGridClearAsync()
    {
        var context = CaptureAndClearState();
        RecordAskEndSequence();
        if (!ShouldShowInterruptDialog(context))
        {
            CompletePlcReset();
            return;
        }
        await TryShowInterruptDialogAsync(context.SerialNumber!);
        CompletePlcReset();
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

    private async Task TryShowInterruptDialogAsync(string serialNumber)
    {
        if (!TryAcquireDialogLock())
        {
            CancelActiveDialog();
            return;
        }
        var cts = new CancellationTokenSource();
        var oldCts = Interlocked.Exchange(ref _interruptDialogCts, cts);
        DisposeOldCts(oldCts);

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

    private void CancelActiveDialog()
    {
        var currentCts = Volatile.Read(ref _interruptDialogCts);
        SafeCancel(currentCts);
    }

    private void ReleaseDialogLock(CancellationTokenSource cts)
    {
        Interlocked.Exchange(ref _interruptDialogActive, 0);
        Interlocked.CompareExchange(ref _interruptDialogCts, null, cts);
        cts.Dispose();
    }

    private void ResetInterruptState()
    {
        Interlocked.Exchange(ref _interruptDialogActive, 0);
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
            var operatorUsername = state.OperatorState.Username ?? "Unknown";

            var result = await coordinators.DialogCoordinator.ShowInterruptReasonDialogAsync(
                serialNumber,
                (admin, reason, token) => infra.InterruptReasonRouter.SaveAsync(
                    serialNumber, admin, reason, useMes, token),
                useMes,
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
        BeginPlcReset();
        HandleStopSignal(PreExecutionResolution.SoftStop);
    }

    private void HandleHardReset()
    {
        TryCompletePlcReset();
        HandleStopSignal(PreExecutionResolution.HardReset);
        infra.StatusReporter.ClearAllExceptScan();
    }

    private void SignalResolution(PreExecutionResolution resolution)
    {
        infra.ErrorService.ClearActiveApplicationErrors();
        _externalSignal?.TrySetResult(resolution);
    }

    #endregion
}
