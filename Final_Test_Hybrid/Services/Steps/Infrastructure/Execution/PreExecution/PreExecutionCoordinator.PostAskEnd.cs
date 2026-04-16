using System.Diagnostics.CodeAnalysis;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public partial class PreExecutionCoordinator
{
    private async Task HandlePostAskEndDecisionAsync(ResetAskEndWindow window)
    {
        var wasTestRunning = state.BoilerState.IsTestRunning;
        var serialNumber = state.BoilerState.SerialNumber;

        // По новому сценарию AskEnd только подтверждает PLC-reset.
        // Cleanup и разблокировка будут позже, после решения PLC.
        coordinators.CompletionUiState.ShowImage(2);

        var postAskEndToken = GetPostAskEndToken();
        var shouldRepeat = await WaitRepeatDecisionAfterAskEndAsync(postAskEndToken);
        if (shouldRepeat)
        {
            if (wasTestRunning)
            {
                await SaveInterruptReasonBeforeRepeatAsync(serialNumber, window.Sequence, postAskEndToken);
            }

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

    private async Task SaveInterruptReasonBeforeRepeatAsync(
        string? serialNumber,
        int expectedSequence,
        CancellationToken ct)
    {
        if (serialNumber == null || !infra.AppSettings.UseInterruptReason)
        {
            return;
        }

        var saveCallback = CreateRepeatSaveCallback(serialNumber);
        coordinators.CompletionUiState.HideImage();
        var result = await TryShowInterruptDialogAsync(
            serialNumber,
            expectedSequence,
            ct,
            showCancelButton: true,
            allowRepeatBypassOnCancel: true,
            saveCallback: saveCallback);
        ct.ThrowIfCancellationRequested();
        MarkInterruptDialogCompletedInCurrentSeries(result);
        if (result.IsSuccess)
        {
            return;
        }

        if (result.IsRepeatBypass)
        {
            infra.Logger.LogWarning(
                "Repeat-save bypass: повтор будет запущен без сохранения в backend для {SerialNumber}",
                serialNumber);
            return;
        }

        throw new InvalidOperationException(
            "Repeat-save dialog завершился без успешного сохранения причины.");
    }

    private Func<string, string, CancellationToken, Task<SaveResult>> CreateRepeatSaveCallback(
        string serialNumber)
    {
        return (adminUsername, reason, ct) => SaveRepeatInterruptThenStartOperationAsync(
            serialNumber,
            adminUsername,
            reason,
            ct);
    }

    private async Task<SaveResult> SaveRepeatInterruptThenStartOperationAsync(
        string serialNumber,
        string adminUsername,
        string reason,
        CancellationToken ct)
    {
        var saveResult = await SaveRepeatInterruptAsync(
            serialNumber,
            adminUsername,
            reason,
            ct);
        if (!saveResult.IsSuccess)
        {
            return saveResult;
        }

        return await StartRepeatOperationAsync(serialNumber, ct);
    }

    private async Task<SaveResult> SaveRepeatInterruptAsync(
        string serialNumber,
        string adminUsername,
        string reason,
        CancellationToken ct)
    {
        if (HasPendingRepeatOperationState(serialNumber))
        {
            return SaveResult.Success();
        }

        var saveResult = await infra.InterruptReasonRouter.SaveAsync(
            serialNumber,
            adminUsername,
            reason,
            infra.AppSettings.UseMes,
            ct);
        if (saveResult.IsSuccess)
        {
            MarkPendingRepeatOperationState(serialNumber);
        }

        return saveResult;
    }

    private async Task<SaveResult> StartRepeatOperationAsync(string serialNumber, CancellationToken ct)
    {
        if (infra.AppSettings.UseMes)
        {
            return await StartMesRepeatOperationAsync(serialNumber, ct);
        }

        return await StartLocalRepeatOperationAsync(ct);
    }

    private async Task<SaveResult> StartMesRepeatOperationAsync(string serialNumber, CancellationToken ct)
    {
        if (steps.GetScanStep() is not ScanBarcodeMesStep mesStep)
        {
            infra.Logger.LogError(
                "Post-AskEnd repeat: MES шаг недоступен для старта новой операции");
            return SaveResult.Fail("Ошибка конфигурации старта операции");
        }

        var startResult = await mesStep.StartRepeatOperationAsync(serialNumber, ct);
        if (startResult.IsSuccess)
        {
            ClearPendingRepeatOperationState();
            return SaveResult.Success();
        }

        infra.Logger.LogWarning(
            "Post-AskEnd repeat: новая операция не запущена для {SerialNumber}: {Error}",
            serialNumber,
            startResult.ErrorMessage);
        return SaveResult.Fail(startResult.ErrorMessage ?? "Ошибка старта операции");
    }

    private async Task<SaveResult> StartLocalRepeatOperationAsync(CancellationToken ct)
    {
        if (steps.GetScanStep() is not ScanBarcodeStep scanStep)
        {
            infra.Logger.LogError(
                "Post-AskEnd repeat: local scan шаг недоступен для старта новой операции");
            return SaveResult.Fail("Ошибка конфигурации старта операции");
        }

        var startResult = await scanStep.StartRepeatOperationAsync(ct);
        if (startResult.IsSuccess)
        {
            ClearPendingRepeatOperationState();
            return SaveResult.Success();
        }

        infra.Logger.LogWarning(
            "Post-AskEnd repeat: локальная операция не запущена: {Error}",
            startResult.ErrorMessage);
        return startResult;
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
            if (infra.OpcSubscription.TryGetValue<bool>(BaseTags.ErrorRetry, out var shouldRepeat) && shouldRepeat)
            {
                infra.Logger.LogInformation("Post-AskEnd: Req_Repeat = true");
                return true;
            }

            if (infra.OpcSubscription.TryGetValue<bool>(BaseTags.AskEnd, out var askEnd) && !askEnd)
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

        // Предыдущий тест считается завершённым только после PLC outcome.
        state.BoilerState.SetTestRunning(false);
        ClearForRepeat();
        _skipNextScan = true;
        SetPostAskEndScanModeDecision(PostAskEndScanModeDecisionRepeat);

        try
        {
            CompletePlcResetOrLogStale(expectedSequence);
        }
        finally
        {
            FinishPostAskEndFlow();
        }
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

        if (HasPendingRepeatOperationState(serialNumber))
        {
            infra.Logger.LogInformation(
                "Post-AskEnd cleanup: причина уже сохранена для {SerialNumber}, повторный диалог пропущен",
                serialNumber);
            FinalizeResetCleanup(expectedSequence, InterruptFlowResult.Success(string.Empty));
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
        MarkInterruptDialogCompletedInCurrentSeries(result);
        FinalizeResetCleanup(expectedSequence, result);
    }

    private void FinalizeResetCleanup(int expectedSequence, InterruptFlowResult? interruptResult = null)
    {
        coordinators.CompletionUiState.HideImage();
        try
        {
            if (!TryRunResetCleanupOnce())
            {
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

            SetPostAskEndScanModeDecision(PostAskEndScanModeDecisionTransitionToReady);
        }
        finally
        {
            try
            {
                CompletePlcResetOrLogStale(expectedSequence);
            }
            finally
            {
                FinishPostAskEndFlow();
            }
        }
    }

    private void StartPostAskEndFlow()
    {
        CancelPostAskEndFlow();

        var cts = new CancellationTokenSource();
        _postAskEndCts = cts;
        SetPostAskEndScanModeDecision(PostAskEndScanModeDecisionNone);
        Volatile.Write(ref _postAskEndActive, 1);
        _runtimeTerminalState.SetPostAskEndActive(true);
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
        _runtimeTerminalState.SetPostAskEndActive(false);
        OnStateChanged?.Invoke();
    }

    private void CancelPostAskEndFlow()
    {
        CancelPostAskEndFlow(PostAskEndScanModeDecisionNone);
    }

    private void CancelPostAskEndFlow(int scanModeDecision)
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
        SetPostAskEndScanModeDecision(scanModeDecision);
        Volatile.Write(ref _postAskEndActive, 0);
        _runtimeTerminalState.SetPostAskEndActive(false);
        OnStateChanged?.Invoke();
    }

    private void AbortPostAskEndFlowForHardReset()
    {
        CancelPostAskEndFlow(PostAskEndScanModeDecisionTransitionToReady);
    }

    private void SetPostAskEndScanModeDecision(int decision)
    {
        Volatile.Write(ref _postAskEndScanModeDecision, decision);
    }

    private void EnsurePostAskEndFlowReleased(int expectedSequence)
    {
        if (Volatile.Read(ref _postAskEndActive) == 0)
        {
            return;
        }

        infra.Logger.LogWarning(
            "Post-AskEnd flow закрыт через fail-safe release: seq={ResetSequence}",
            expectedSequence);
        ResetInterruptState();
        CancelPostAskEndFlow();
    }

    private void MarkInterruptDialogCompletedInCurrentSeries(InterruptFlowResult result)
    {
        // Право на повторный показ тратим только если оператор
        // реально завершил окно: сохранил причину, нажал Cancel
        // или подтвердил аварийный RepeatBypass.
        // Принудительное закрытие новым reset сюда не попадает.
        if (!result.IsSuccess && !result.IsCancelled && !result.IsRepeatBypass)
        {
            return;
        }

        Interlocked.Exchange(ref _interruptDialogCompletedInCurrentResetSeries, 1);
        Volatile.Write(ref _interruptDialogAllowedSequence, 0);

        infra.Logger.LogInformation(
            "InterruptReasonDialogSeriesCompleted: success={IsSuccess}, cancelled={IsCancelled}, repeatBypass={IsRepeatBypass}",
            result.IsSuccess,
            result.IsCancelled,
            result.IsRepeatBypass);
    }
}
