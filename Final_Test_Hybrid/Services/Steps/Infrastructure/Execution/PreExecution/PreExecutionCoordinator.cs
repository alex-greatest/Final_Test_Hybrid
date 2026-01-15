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
    }

    public void SubmitBarcode(string barcode)
    {
        _barcodeSource?.TrySetResult(barcode);
    }

    private void SetAcceptingInput(bool value)
    {
        IsAcceptingInput = value;
        OnStateChanged?.Invoke();
    }

    public ScanStepBase GetScanStep() => steps.GetScanStep();
}
