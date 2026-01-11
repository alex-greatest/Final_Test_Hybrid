using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Manages barcode scanning workflow: session control, barcode processing, and error handling.
/// Coordinates between scanner hardware, PreExecution pipeline, and UI notifications.
/// </summary>
public class ScanStepManager : IDisposable
{
    private readonly ScanSessionManager _sessionManager;
    private readonly ScanStateManager _scanStateManager;
    private readonly ScanModeController _modeController;
    private readonly ScanDialogCoordinator _dialogCoordinator;
    private readonly PreExecutionCoordinator _preExecutionCoordinator;
    private readonly TestExecutionCoordinator _coordinator;
    private readonly ILogger<ScanStepManager> _logger;
    private bool _disposed;

    /// <summary>
    /// Текущее состояние state machine.
    /// </summary>
    public ScanState State => _scanStateManager.State;

    /// <summary>
    /// Идёт ли обработка штрихкода (для обратной совместимости).
    /// </summary>
    public bool IsProcessing => _scanStateManager.State is ScanState.Processing or ScanState.TestRunning;

    /// <summary>
    /// Можно ли принимать ввод штрихкода.
    /// </summary>
    public bool CanAcceptInput => _scanStateManager.CanAcceptInput;

    /// <summary>
    /// Текущий штрихкод.
    /// </summary>
    public string? CurrentBarcode => _scanStateManager.CurrentBarcode;

    /// <summary>
    /// Очистить текущий штрихкод.
    /// </summary>
    public void ClearBarcode() => _scanStateManager.ClearBarcode();

    /// <summary>
    /// Событие изменения состояния.
    /// </summary>
    public event Action? OnChange
    {
        add => _scanStateManager.OnStateChanged += value;
        remove => _scanStateManager.OnStateChanged -= value;
    }

    // Делегируем события диалогов к ScanDialogCoordinator
    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested
    {
        add => _dialogCoordinator.OnMissingPlcTagsDialogRequested += value;
        remove => _dialogCoordinator.OnMissingPlcTagsDialogRequested -= value;
    }
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested
    {
        add => _dialogCoordinator.OnMissingRequiredTagsDialogRequested += value;
        remove => _dialogCoordinator.OnMissingRequiredTagsDialogRequested -= value;
    }
    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested
    {
        add => _dialogCoordinator.OnUnknownStepsDialogRequested += value;
        remove => _dialogCoordinator.OnUnknownStepsDialogRequested -= value;
    }
    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested
    {
        add => _dialogCoordinator.OnMissingRecipesDialogRequested += value;
        remove => _dialogCoordinator.OnMissingRecipesDialogRequested -= value;
    }
    public event Func<IReadOnlyList<RecipeWriteErrorInfo>, Task>? OnRecipeWriteErrorDialogRequested
    {
        add => _dialogCoordinator.OnRecipeWriteErrorDialogRequested += value;
        remove => _dialogCoordinator.OnRecipeWriteErrorDialogRequested -= value;
    }
    public event Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkDialogRequested
    {
        add => _dialogCoordinator.OnReworkDialogRequested += value;
        remove => _dialogCoordinator.OnReworkDialogRequested -= value;
    }

    public ScanStepManager(
        ScanSessionManager sessionManager,
        ScanStateManager scanStateManager,
        ScanModeController modeController,
        ScanDialogCoordinator dialogCoordinator,
        PreExecutionCoordinator preExecutionCoordinator,
        TestExecutionCoordinator coordinator,
        ILogger<ScanStepManager> logger)
    {
        _sessionManager = sessionManager;
        _scanStateManager = scanStateManager;
        _modeController = modeController;
        _dialogCoordinator = dialogCoordinator;
        _preExecutionCoordinator = preExecutionCoordinator;
        _coordinator = coordinator;
        _logger = logger;
        _coordinator.OnSequenceCompleted += HandleSequenceCompleted;
        _modeController.Initialize(HandleBarcodeScanned);
    }

    private async void HandleBarcodeScanned(string barcode)
    {
        try
        {
            if (_disposed)
            {
                return;
            }
            await ProcessBarcodeWithLoggingAsync(barcode);
        }
        catch (Exception)
        {
            // ignored
        }
    }

    private async Task ProcessBarcodeWithLoggingAsync(string barcode)
    {
        try
        {
            await ProcessBarcodeAsync(barcode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанная ошибка при сканировании: {Barcode}", barcode);
        }
    }

    public async Task ProcessBarcodeAsync(string barcode)
    {
        if (!_scanStateManager.TryAcquireProcessLock())
        {
            return;
        }
        try
        {
            await ExecuteBarcodeProcessing(barcode);
        }
        finally
        {
            _scanStateManager.ReleaseProcessLock();
        }
    }

    private async Task ExecuteBarcodeProcessing(string barcode)
    {
        _scanStateManager.SetBarcode(barcode);
        TransitionToProcessing();
        var status = await ProcessBarcodeWithErrorHandlingAsync(barcode);
        HandlePostProcessingTransition(status);
    }

    private void TransitionToProcessing()
    {
        _scanStateManager.TryTransitionTo(ScanState.Processing, () =>
        {
            _sessionManager.ReleaseSession();
        });
    }

    private void HandlePostProcessingTransition(PreExecutionStatus status)
    {
        if (status == PreExecutionStatus.TestStarted)
        {
            _scanStateManager.TryTransitionTo(ScanState.TestRunning);
            return;
        }
        _modeController.TransitionToReady();
    }

    private async Task<PreExecutionStatus> ProcessBarcodeWithErrorHandlingAsync(string barcode)
    {
        try
        {
            return await ProcessPreExecutionAsync(barcode);
        }
        catch (Exception ex)
        {
            HandleCriticalError(ex);
            return PreExecutionStatus.Failed;
        }
    }

    private async Task<PreExecutionStatus> ProcessPreExecutionAsync(string barcode)
    {
        var scanStepId = _scanStateManager.ActiveScanStepId;
        var result = await _preExecutionCoordinator.ExecuteAsync(barcode, scanStepId, CancellationToken.None);
        await HandlePreExecutionResultAsync(result);
        return result.Status;
    }

    private async Task HandlePreExecutionResultAsync(PreExecutionResult result)
    {
        switch (result.Status)
        {
            case PreExecutionStatus.TestStarted:
            case PreExecutionStatus.Continue:
                return;
            case PreExecutionStatus.Failed:
            case PreExecutionStatus.Cancelled:
                await _dialogCoordinator.HandlePreExecutionErrorAsync(result);
                return;
            default:
                throw new InvalidOperationException($"Неизвестный статус PreExecution: {result.Status}");
        }
    }

    private void HandleCriticalError(Exception ex)
    {
        _logger.LogError(ex, "Критическая ошибка при запуске теста");
    }

    private void HandleSequenceCompleted()
    {
        LogSequenceCompleted();
        _scanStateManager.SetActiveScanStepId(null);
        _scanStateManager.ClearBarcode();
        _modeController.TransitionToReady();
        _dialogCoordinator.ShowCompletionNotification(_coordinator.HasErrors);
    }

    private void LogSequenceCompleted()
    {
        _logger.LogInformation("Последовательность завершена. Ошибки: {HasErrors}", _coordinator.HasErrors);
    }

    public void Dispose()
    {
        _disposed = true;
        _sessionManager.ReleaseSession();
        _coordinator.OnSequenceCompleted -= HandleSequenceCompleted;
        _modeController.Dispose();
    }
}
