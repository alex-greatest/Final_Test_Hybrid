using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;

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
    private sealed class ResetAskEndWindow
    {
        public ResetAskEndWindow(int sequence)
        {
            Sequence = sequence;
            Signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public int Sequence { get; }
        public TaskCompletionSource Signal { get; }
    }

    // === Состояние ввода ===
    private TaskCompletionSource<string>? _barcodeSource;
    private CancellationTokenSource? _currentCts;
    private CycleExitReason? _pendingExitReason;
    private TaskCompletionSource<CycleExitReason>? _resetSignal;
    private ResetAskEndWindow? _currentAskEndWindow;
    private CancellationTokenSource _resetCts = new();
    private int _resetSequence;
    private int _resetCleanupDone;
    private int _interruptReasonUsedInCurrentResetSeries;
    private int _interruptDialogAllowedSequence;
    private int _interruptReasonDialogSequence;
    private bool _skipNextScan;
    private bool _executeFullPreparation;
    private PreExecutionContext? _lastSuccessfulContext;
    private const string ScanBarcodeTestName = "ScanBarcode";
    private const string AppVersionResult = "App_Version";
    private const string PlantIdResult = "Plant_ID";
    private const string ShiftNoResult = "Shift_No";
    private const string TesterNoResult = "Tester_No";
    private const string PresAtmosphResult = "Pres_atmosph.";
    private const string PresInGasResult = "Pres_in_gas";

    // === Отслеживание источника reset ===
    private const int ResetOriginPlc = 1;
    private const int ResetOriginNonPlc = 2;
    private int _lastHardResetOrigin;

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
        infra.ErrorService.IsHistoryEnabled = false;
        infra.StepTimingService.Clear();
        infra.RecipeProvider.Clear();
        _lastSuccessfulContext = null;
        ClearBarcode();

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
        _lastSuccessfulContext = null;

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

    private int BeginResetCycle(int origin, bool ensureAskEndWindow)
    {
        var resetSequence = Interlocked.Increment(ref _resetSequence);
        ArmResetCleanupGuard();
        infra.Logger.LogDebug(
            "Старт reset-цикла: seq={ResetSequence}, source={ResetSource}, cleanupArmed={CleanupArmed}",
            resetSequence,
            DescribeResetOrigin(origin),
            true);
        if (ensureAskEndWindow)
        {
            EnsureAskEndWindow(resetSequence);
            CancelResetToken();
        }
        return resetSequence;
    }

    private int BeginPlcReset()
    {
        return BeginResetCycle(ResetOriginPlc, ensureAskEndWindow: true);
    }

    private bool CompletePlcReset(int expectedSequence)
    {
        if (!TryTakeAskEndWindow(expectedSequence, out var window) || window == null)
        {
            return false;
        }
        ReplaceResetToken();
        window.Signal.TrySetResult();
        return true;
    }

    private void TryCompletePlcReset()
    {
        var window = Interlocked.Exchange(ref _currentAskEndWindow, null);
        if (window == null)
        {
            return;
        }
        ReplaceResetToken();
        window.Signal.TrySetResult();
    }

    private async Task WaitForAskEndIfNeededAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var window = Volatile.Read(ref _currentAskEndWindow);
            if (window == null)
            {
                return;
            }
            try
            {
                await window.Signal.Task.WaitAsync(ct);
                if (ReferenceEquals(window, Volatile.Read(ref _currentAskEndWindow)))
                {
                    return;
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (ReferenceEquals(window, Volatile.Read(ref _currentAskEndWindow)))
                {
                    return;
                }
            }
        }
    }

    private bool DidResetOccur(int sequenceSnapshot) =>
        sequenceSnapshot != Volatile.Read(ref _resetSequence);

    private static string DescribeResetOrigin(int origin)
    {
        return origin switch
        {
            ResetOriginPlc => "PLC",
            ResetOriginNonPlc => "NonPlc",
            _ => "Unknown"
        };
    }

    private void EnsureAskEndWindow(int resetSequence)
    {
        var nextWindow = new ResetAskEndWindow(resetSequence);
        var previousWindow = Interlocked.Exchange(ref _currentAskEndWindow, nextWindow);
        previousWindow?.Signal.TrySetCanceled();
    }

    private void ReplaceResetToken()
    {
        var previousCts = Interlocked.Exchange(ref _resetCts, new CancellationTokenSource());
        previousCts.Dispose();
    }

    private bool TryTakeAskEndWindow(int expectedSequence, out ResetAskEndWindow? window)
    {
        var currentWindow = Volatile.Read(ref _currentAskEndWindow);
        if (currentWindow == null || currentWindow.Sequence != expectedSequence)
        {
            window = null;
            return false;
        }
        if (Interlocked.CompareExchange(ref _currentAskEndWindow, null, currentWindow) != currentWindow)
        {
            window = null;
            return false;
        }
        window = currentWindow;
        return true;
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

    private void ArmResetCleanupGuard()
    {
        Interlocked.Exchange(ref _resetCleanupDone, 0);
    }

    private bool TryRunResetCleanupOnce()
    {
        return Interlocked.CompareExchange(ref _resetCleanupDone, 1, 0) == 0;
    }

    private async Task<PreExecutionResult?> WriteScanServiceResultsAsync(PreExecutionContext context, CancellationToken ct)
    {
        var scanContext = context.ScanServiceContext;
        if (scanContext == null)
        {
            infra.Logger.LogError("Scan-контекст отсутствует: barcode={Barcode}", context.Barcode);
            return PreExecutionResult.Fail("Отсутствуют scan-данные для старта теста");
        }

        SaveScanServiceResult(AppVersionResult, scanContext.AppVersion, "");
        SaveScanServiceResult(PlantIdResult, scanContext.PlantId, "");
        SaveScanServiceResult(ShiftNoResult, scanContext.ShiftNo, "");
        SaveScanServiceResult(TesterNoResult, scanContext.TesterNo, "");

        var pressures = await steps.GetScanStep().ReadPressuresAsync(ct);
        if (!pressures.Success)
        {
            return pressures.Error;
        }

        SaveScanServiceResult(PresAtmosphResult, pressures.GasPa.ToString(System.Globalization.CultureInfo.InvariantCulture), "бар");
        SaveScanServiceResult(PresInGasResult, pressures.GasP.ToString(System.Globalization.CultureInfo.InvariantCulture), "мбар");

        return null;
    }

    private void SaveScanServiceResult(string parameterName, string value, string unit)
    {
        infra.TestResultsService.Remove(parameterName);
        infra.TestResultsService.Add(
            parameterName: parameterName,
            value: value,
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: unit,
            test: ScanBarcodeTestName);
    }
}
