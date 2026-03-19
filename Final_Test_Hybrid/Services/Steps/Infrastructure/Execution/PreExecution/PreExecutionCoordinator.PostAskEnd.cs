using System.Diagnostics.CodeAnalysis;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private async Task HandlePostAskEndDecisionAsync()
    {
        if (!TryGetCurrentAskEndWindow(out var window))
        {
            return;
        }

        // AskEnd больше не считается финалом PLC reset-сценария.
        // Changeover и финальная разблокировка разрешаются только
        // после завершения post-AskEnd ветки.
        StartPostAskEndFlow();

        var wasTestRunning = state.BoilerState.IsTestRunning;
        var serialNumber = state.BoilerState.SerialNumber;

        // По новому сценарию AskEnd только подтверждает PLC-reset.
        // Cleanup и разблокировка будут позже, после решения PLC.
        coordinators.CompletionUiState.ShowImage(2);

        var postAskEndToken = GetPostAskEndToken();
        var shouldRepeat = await WaitRepeatDecisionAfterAskEndAsync(postAskEndToken);
        if (shouldRepeat)
        {
            await AcknowledgeRepeatRequestAsync(postAskEndToken);
            if (!wasTestRunning)
            {
                FinalizeResetCleanup(window.Sequence);
                return;
            }

            StartRepeatAfterReset(window.Sequence);
            return;
        }

        if (!wasTestRunning)
        {
            FinalizeResetCleanup(window.Sequence);
            return;
        }

        await ShowInterruptReasonThenCleanupAsync(serialNumber, window.Sequence, postAskEndToken);
    }

    private bool TryGetCurrentAskEndWindow([NotNullWhen(true)] out ResetAskEndWindow? window)
    {
        window = Volatile.Read(ref _currentAskEndWindow);
        var resetSequence = GetResetSequenceSnapshot();
        if (window == null)
        {
            LogAskEndIgnoredAsStale(0, resetSequence);
            return false;
        }

        if (window.Sequence == resetSequence)
        {
            return true;
        }

        LogAskEndIgnoredAsStale(window.Sequence, resetSequence);
        window = null;
        return false;
    }

    private async Task<bool> WaitRepeatDecisionAfterAskEndAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (infra.OpcSubscription.GetValue<bool>(BaseTags.ErrorRetry))
            {
                infra.Logger.LogInformation("Post-AskEnd: Req_Repeat = true");
                return true;
            }

            if (!infra.OpcSubscription.GetValue<bool>(BaseTags.AskEnd))
            {
                infra.Logger.LogInformation("Post-AskEnd: AskEnd сброшен без Req_Repeat");
                return false;
            }

            await Task.Delay(200, ct);
        }

        throw new OperationCanceledException(ct);
    }

    private async Task AcknowledgeRepeatRequestAsync(CancellationToken ct)
    {
        await coordinators.ErrorCoordinator.SendAskRepeatAsync(ct);
        infra.Logger.LogInformation("Post-AskEnd: AskRepeat = true");
    }

    private void StartRepeatAfterReset(int expectedSequence)
    {
        coordinators.CompletionUiState.HideImage();

        // Repeat и changeover взаимоисключающие пути.
        StopChangeoverAndAllowRestart();
        ClearChangeoverPendingState();

        ClearForRepeat();
        _skipNextScan = true;
        Volatile.Write(ref _postAskEndScanModeDecision, 2);

        CompletePlcResetOrLogStale(expectedSequence);
        FinishPostAskEndFlow();
    }

    private async Task ShowInterruptReasonThenCleanupAsync(
        string? serialNumber,
        int expectedSequence,
        CancellationToken ct)
    {
        coordinators.CompletionUiState.HideImage();
        if (serialNumber == null || !infra.AppSettings.UseInterruptReason)
        {
            FinalizeResetCleanup(expectedSequence);
            return;
        }

        if (Volatile.Read(ref _interruptDialogAllowedSequence) != expectedSequence)
        {
            LogInterruptDialogSuppressed(
                "series_latch",
                expectedSequence,
                Volatile.Read(ref _interruptDialogAllowedSequence));
            FinalizeResetCleanup(expectedSequence);
            return;
        }

        var result = await TryShowInterruptDialogAsync(serialNumber, expectedSequence, ct);
        ct.ThrowIfCancellationRequested();
        FinalizeResetCleanup(expectedSequence, result);
    }

    private void FinalizeResetCleanup()
    {
        FinalizeResetCleanup(GetResetSequenceSnapshot());
    }

    private void FinalizeResetCleanup(int expectedSequence, InterruptFlowResult? interruptResult = null)
    {
        coordinators.CompletionUiState.HideImage();
        if (!TryRunResetCleanupOnce())
        {
            CompletePlcResetOrLogStale(expectedSequence);
            FinishPostAskEndFlow();
            return;
        }

        var changeoverMode = GetChangeoverResetMode();
        ClearStateOnReset();
        infra.StatusReporter.ClearAllExceptScan(SequenceClearMode.OperationalReset);

        // Только здесь AskEnd становится завершённой стадией для changeover.
        RecordAskEndSequence(expectedSequence);
        HandleChangeoverAfterReset(changeoverMode);
        if (interruptResult != null)
        {
            HandleChangeoverAfterInterrupt(interruptResult);
        }

        Volatile.Write(ref _postAskEndScanModeDecision, 1);
        CompletePlcResetOrLogStale(expectedSequence);
        FinishPostAskEndFlow();
    }

    private void StartPostAskEndFlow()
    {
        CancelPostAskEndFlow();

        var cts = new CancellationTokenSource();
        _postAskEndCts = cts;
        Volatile.Write(ref _postAskEndScanModeDecision, 0);
        Volatile.Write(ref _postAskEndActive, 1);
        OnStateChanged?.Invoke();
    }

    private CancellationToken GetPostAskEndToken()
    {
        var cts = _postAskEndCts;
        return cts?.Token ?? throw new InvalidOperationException("Post-AskEnd CTS не инициализирован");
    }

    private void FinishPostAskEndFlow()
    {
        var cts = Interlocked.Exchange(ref _postAskEndCts, null);
        cts?.Dispose();

        Volatile.Write(ref _postAskEndActive, 0);
        OnStateChanged?.Invoke();
    }

    private void CancelPostAskEndFlow()
    {
        var cts = Interlocked.Exchange(ref _postAskEndCts, null);
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

        cts.Dispose();
        coordinators.CompletionUiState.HideImage();
        CancelActiveDialog();
        Volatile.Write(ref _postAskEndScanModeDecision, 0);
        Volatile.Write(ref _postAskEndActive, 0);
        OnStateChanged?.Invoke();
    }
}
