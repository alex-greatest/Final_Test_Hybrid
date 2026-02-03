namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Обработчики выхода из цикла PreExecution.
/// </summary>
public partial class PreExecutionCoordinator
{
    private void HandleTestCompletedExit()
    {
        state.BoilerState.SetTestRunning(false);
        ClearForTestCompletion();
        StartChangeoverTimerForImmediateReset();
    }

    private void HandleSoftResetExit()
    {
        // Очистка произойдёт по AskEnd в HandleGridClear
        HandleChangeoverAfterReset(GetChangeoverResetMode());
    }

    private void HandleHardResetExit()
    {
        // ВАЖНО: snapshot changeoverMode ДО очистки состояния
        var changeoverMode = GetChangeoverResetMode();
        ClearStateOnReset();
        infra.StatusReporter.ClearAllExceptScan();
        HandleChangeoverAfterReset(changeoverMode);
        TrySendSyntheticChangeoverSignals(changeoverMode);
    }

    private void HandleRepeatRequestedExit()
    {
        ClearForRepeat();
        _skipNextScan = true;
    }

    private void HandleNokRepeatRequestedExit()
    {
        ClearForNokRepeat();
        _skipNextScan = true;
        _executeFullPreparation = true;
    }
}
