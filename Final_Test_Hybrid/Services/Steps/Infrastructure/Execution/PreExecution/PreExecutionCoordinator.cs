using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

/// <summary>
/// Причина выхода из цикла PreExecution.
/// Используется для явного управления очисткой состояния.
/// </summary>
public enum CycleExitReason
{
    PipelineFailed,        // Pipeline вернул ошибку
    PipelineCancelled,     // Pipeline отменён (не сброс)
    TestCompleted,         // Тест завершился нормально
    SoftReset,             // Мягкий сброс (wasInScanPhase = true)
    HardReset,             // Жёсткий сброс
    RepeatRequested,       // OK повтор теста
    NokRepeatRequested,    // NOK повтор с подготовкой
}

/// <summary>
/// Упрощённый координатор PreExecution.
/// Выполняет только два шага: ScanStep (вся подготовка) и BlockBoilerAdapterStep.
/// </summary>
public partial class PreExecutionCoordinator(
    PreExecutionSteps steps,
    PreExecutionInfrastructure infra,
    PreExecutionCoordinators coordinators,
    PreExecutionState state)
{
    // === Состояние ввода ===
    private TaskCompletionSource<string>? _barcodeSource;
    private CancellationTokenSource? _currentCts;
    private CycleExitReason? _pendingExitReason;
    private TaskCompletionSource<CycleExitReason>? _resetSignal;
    private TaskCompletionSource? _askEndSignal;
    private CancellationTokenSource _resetCts = new();
    private int _resetSequence;
    private bool _skipNextScan;
    private bool _executeFullPreparation;
    private PreExecutionContext? _lastSuccessfulContext;
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

    private enum ChangeoverResetMode
    {
        Immediate,
        WaitForReason,
        WaitForAskEndOnly
    }

    public bool IsAcceptingInput { get; private set; }
    public bool IsProcessing => !IsAcceptingInput && state.ActivityTracker.IsPreExecutionActive;
    public string? CurrentBarcode { get; private set; }
    public event Action? OnStateChanged;

    public void ClearBarcode()
    {
        CurrentBarcode = null;
        OnStateChanged?.Invoke();
    }

    private void ClearStateOnReset()
    {
        state.BoilerState.Clear();
        state.PhaseState.Clear();
        ClearBarcode();
        infra.ErrorService.IsHistoryEnabled = false;
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        _lastSuccessfulContext = null;

        infra.Logger.LogInformation("Состояние очищено при сбросе");
    }

    /// <summary>
    /// Очистка при завершении теста (OK/NOK).
    /// Результаты и история ошибок НЕ чистятся — оператор должен их видеть.
    /// </summary>
    private void ClearForTestCompletion()
    {
        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        state.BoilerState.Clear();
        ClearBarcode();
        infra.ErrorService.IsHistoryEnabled = false;

        infra.Logger.LogInformation("Состояние очищено после завершения теста");
    }

    /// <summary>
    /// Очистка при начале нового теста.
    /// Вызывается перед включением IsHistoryEnabled для очистки данных от предыдущего теста.
    /// </summary>
    private void ClearForNewTestStart()
    {
        infra.ErrorService.ClearHistory();
        infra.TestResultsService.Clear();
        infra.StepHistory.Clear();
        infra.TimerService.Clear();
        state.BoilerState.ClearLastTestInfo();

        infra.Logger.LogInformation("История и результаты очищены для нового теста");
    }

    /// <summary>
    /// Добавляет версию приложения в результаты теста.
    /// </summary>
    private void AddAppVersionToResults()
    {
        infra.TestResultsService.Remove("App_Version");
        var appVersion = infra.RecipeProvider.GetStringValue("App_Version");
        if (!string.IsNullOrEmpty(appVersion))
        {
            infra.TestResultsService.Add("App_Version", appVersion, "", "", 1, false, "");
        }
    }

    private void ClearForRepeat()
    {
        infra.ErrorService.IsHistoryEnabled = false;

        // Очистка UI
        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear(preserveScanState: true);

        // История и результаты чистятся в ClearForNewTestStart при запуске pipeline

        // Сброс состояния TestExecutionCoordinator
        coordinators.TestCoordinator.ResetForRepeat();

        infra.Logger.LogInformation("Состояние очищено для повтора");
    }

    private void ClearForNokRepeat()
    {
        // Очистка состояния котла (но не CurrentBarcode!)
        state.BoilerState.Clear();
        state.PhaseState.Clear();
        infra.ErrorService.IsHistoryEnabled = false;
        _lastSuccessfulContext = null;

        // Очистка UI
        infra.StatusReporter.ClearAllExceptScan();
        infra.StepTimingService.Clear(preserveScanState: true);
        infra.RecipeProvider.Clear();

        // История и результаты чистятся в ClearForNewTestStart при запуске pipeline

        // Сброс состояния TestExecutionCoordinator
        coordinators.TestCoordinator.ResetForRepeat();

        infra.Logger.LogInformation("Состояние очищено для NOK повтора с подготовкой");
    }

    public void SubmitBarcode(string barcode)
    {
        _barcodeSource?.TrySetResult(barcode);
    }

    private void SetAcceptingInput(bool value)
    {
        IsAcceptingInput = value;
        if (value)
        {
            infra.StepTimingService.ResetScanTiming();
        }
        OnStateChanged?.Invoke();
    }

    public ScanStepBase GetScanStep() => steps.GetScanStep();

    private void BeginPlcReset()
    {
        Interlocked.Increment(ref _resetSequence);
        EnsureAskEndSignal();
        CancelResetToken();
    }

    private void CompletePlcReset()
    {
        var previousCts = Interlocked.Exchange(ref _resetCts, new CancellationTokenSource());
        previousCts.Dispose();
        var signal = Interlocked.Exchange(ref _askEndSignal, null);
        signal?.TrySetResult();
    }

    private void TryCompletePlcReset()
    {
        var signal = Interlocked.Exchange(ref _askEndSignal, null);
        if (signal == null) return;
        // Swap CTS to signal reset; disposal may trigger ODE in waiters and is expected.
        Interlocked.Exchange(ref _resetCts, new CancellationTokenSource()).Dispose();
        signal.TrySetResult();
    }

    private Task WaitForAskEndIfNeededAsync(CancellationToken ct)
    {
        var signal = _askEndSignal;
        return signal == null
            ? Task.CompletedTask
            : signal.Task.WaitAsync(ct);
    }

    private bool DidResetOccur(int sequenceSnapshot) =>
        sequenceSnapshot != Volatile.Read(ref _resetSequence);

    private void EnsureAskEndSignal()
    {
        if (_askEndSignal == null || _askEndSignal.Task.IsCompleted)
        {
            _askEndSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    private void CancelResetToken()
    {
        try
        {
            _resetCts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Expected during shutdown
        }
    }

    /// <summary>
    /// Сигнализирует о сбросе для race condition protection.
    /// </summary>
    private void SignalReset(CycleExitReason reason)
    {
        _resetSignal?.TrySetResult(reason);
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
        return stopReason is ExecutionStopReason.PlcSoftReset or ExecutionStopReason.PlcHardReset or ExecutionStopReason.PlcForceStop
            && !state.BoilerState.IsTestRunning;
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

    private void RecordAskEndSequence()
    {
        var resetSequence = GetResetSequenceSnapshot();
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
        var resetSequence = GetResetSequenceSnapshot();
        switch (mode)
        {
            case ChangeoverResetMode.WaitForReason:
                ArmChangeoverPendingForReset(resetSequence, ChangeoverTriggerReasonSaved);
                break;
            case ChangeoverResetMode.WaitForAskEndOnly:
                ArmChangeoverPendingForReset(resetSequence, ChangeoverTriggerAskEndOnly);
                break;
            default:
                StartChangeoverTimerForImmediateReset();
                break;
        }
    }

    private void StopChangeoverTimerForReset(ChangeoverResetMode mode)
    {
        if (mode == ChangeoverResetMode.Immediate)
        {
            return;
        }
        state.BoilerState.StopChangeoverTimer();
    }

    /// <summary>
    /// Проверяет наличие причины остановки из любого источника.
    /// </summary>
    private bool TryGetStopExitReason(out CycleExitReason reason)
    {
        if (_pendingExitReason.HasValue)
        {
            reason = _pendingExitReason.Value;
            return true;
        }

        // Захватываем локальную копию для защиты от race condition
        var resetSignal = _resetSignal;
        if (resetSignal?.Task.IsCompletedSuccessfully == true)
        {
            reason = resetSignal.Task.Result;
            return true;
        }

        var stopReason = state.FlowState.StopReason;
        if (stopReason == ExecutionStopReason.PlcForceStop)
        {
            reason = state.BoilerState.IsTestRunning
                ? CycleExitReason.HardReset
                : CycleExitReason.SoftReset;
            return true;
        }

        var mapped = MapStopReasonToExitReason(stopReason);
        if (mapped.HasValue)
        {
            reason = mapped.Value;
            return true;
        }

        reason = default;
        return false;
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
            _ => null,
        };
    }
}
