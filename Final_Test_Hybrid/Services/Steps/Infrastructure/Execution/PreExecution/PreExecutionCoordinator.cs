using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

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
    private volatile bool _resetRequested;

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
