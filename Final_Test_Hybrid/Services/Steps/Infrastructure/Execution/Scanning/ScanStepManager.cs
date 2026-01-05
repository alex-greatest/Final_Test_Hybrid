using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Manages barcode scanning workflow: session control, barcode processing, and error handling.
/// Coordinates between scanner hardware, PreExecution pipeline, and UI notifications.
/// </summary>
public class ScanStepManager : IDisposable
{
    private const int MessagePriority = 100;
    private const string ScanPromptMessage = "Отсканируйте серийный номер котла";
    private readonly ScanSessionManager _sessionManager;
    private readonly ScanInputStateManager _inputStateManager;
    private readonly PreExecutionCoordinator _preExecutionCoordinator;
    private readonly ScanErrorHandler _errorHandler;
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly MessageService _messageService;
    private readonly ExecutionMessageState _executionMessageState;
    private readonly TestExecutionCoordinator _coordinator;
    private readonly ITestStepLogger _testStepLogger;
    private readonly ILogger<ScanStepManager> _logger;
    private object? _messageProviderKey;
    private bool _disposed;

    public bool IsProcessing => _inputStateManager.IsProcessing;

    public event Action? OnChange
    {
        add => _inputStateManager.OnStateChanged += value;
        remove => _inputStateManager.OnStateChanged -= value;
    }

    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested;
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested;
    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested;
    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested;
    public event Func<IReadOnlyList<RecipeWriteErrorInfo>, Task>? OnRecipeWriteErrorDialogRequested;
    public event Func<string, Func<string, string, Task<ReworkSubmitResult>>, Task<ReworkFlowResult>>? OnReworkDialogRequested;

    private bool IsScanModeEnabled => _operatorState.IsAuthenticated && _autoReady.IsReady;

    public ScanStepManager(
        ScanSessionManager sessionManager,
        ScanInputStateManager inputStateManager,
        PreExecutionCoordinator preExecutionCoordinator,
        ScanErrorHandler errorHandler,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        MessageService messageService,
        ExecutionMessageState executionMessageState,
        TestExecutionCoordinator coordinator,
        ITestStepLogger testStepLogger,
        IEnumerable<IPreExecutionStep> preExecutionSteps,
        ILogger<ScanStepManager> logger)
    {
        _sessionManager = sessionManager;
        _inputStateManager = inputStateManager;
        _preExecutionCoordinator = preExecutionCoordinator;
        _errorHandler = errorHandler;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _messageService = messageService;
        _executionMessageState = executionMessageState;
        _coordinator = coordinator;
        _testStepLogger = testStepLogger;
        _logger = logger;
        ConfigureReworkCallback(preExecutionSteps);
        SubscribeToEvents();
        UpdateScanModeState();
    }

    private void ConfigureReworkCallback(IEnumerable<IPreExecutionStep> steps)
    {
        var mesStep = steps.OfType<ScanBarcodeMesStep>().FirstOrDefault();
        mesStep?.OnReworkRequired = HandleReworkDialogAsync;
    }

    private async Task<ReworkFlowResult> HandleReworkDialogAsync(
        string errorMessage,
        Func<string, string, Task<ReworkSubmitResult>> executeRework)
    {
        if (OnReworkDialogRequested == null)
        {
            return ReworkFlowResult.Cancelled();
        }
        return await OnReworkDialogRequested(errorMessage, executeRework);
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnChange += HandleStateChange;
        _autoReady.OnChange += HandleStateChange;
        _messageProviderKey = _messageService.RegisterProvider(MessagePriority, GetScanMessage);
        _coordinator.OnSequenceCompleted += HandleSequenceCompleted;
    }

    private string? GetScanMessage()
    {
        return IsScanModeEnabled ? ScanPromptMessage : null;
    }

    private void HandleStateChange()
    {
        UpdateScanModeState();
    }

    private void UpdateScanModeState()
    {
        SetScanModeActive(IsScanModeEnabled);
        _messageService.NotifyChanged();
    }

    private void SetScanModeActive(bool isActive)
    {
        if (isActive)
        {
            ActivateScanMode();
        }
        else
        {
            DeactivateScanMode();
        }
    }

    private void ActivateScanMode()
    {
        _sessionManager.AcquireSession(HandleBarcodeScanned);
        _testStepLogger.StartNewSession();
    }

    private void DeactivateScanMode()
    {
        _sessionManager.ReleaseSession();
    }

    private async void HandleBarcodeScanned(string barcode)
    {
        if (_disposed)
        {
            return;
        }
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
        if (!_inputStateManager.TryAcquireProcessLock())
        {
            return;
        }
        try
        {
            await ExecuteBarcodeProcessing(barcode);
        }
        finally
        {
            _inputStateManager.ReleaseProcessLock();
        }
    }

    private async Task ExecuteBarcodeProcessing(string barcode)
    {
        BlockInput();
        var result = await _preExecutionCoordinator.ExecuteAsync(barcode, CancellationToken.None);
        if (result.Success)
        {
            return;
        }
        await HandlePreExecutionError(result);
        UnblockInput();
    }

    private async Task HandlePreExecutionError(PreExecutionResult result)
    {
        _errorHandler.ShowError("Ошибка", result.ErrorMessage ?? "Неизвестная ошибка");
        await RaiseDetailedErrorDialogAsync(result);
    }

    private async Task RaiseDetailedErrorDialogAsync(PreExecutionResult result)
    {
        var task = result.ErrorDetails switch
        {
            MissingPlcTagsDetails details => OnMissingPlcTagsDialogRequested?.Invoke(details.Tags),
            MissingRequiredTagsDetails details => OnMissingRequiredTagsDialogRequested?.Invoke(details.Tags),
            UnknownStepsDetails details => OnUnknownStepsDialogRequested?.Invoke(details.Steps),
            MissingRecipesDetails details => OnMissingRecipesDialogRequested?.Invoke(details.Recipes),
            RecipeWriteErrorDetails details => OnRecipeWriteErrorDialogRequested?.Invoke(details.Errors),
            _ => null
        };
        await (task ?? Task.CompletedTask);
    }

    private void BlockInput()
    {
        _sessionManager.ReleaseSession();
        _inputStateManager.SetProcessing(true);
    }

    private void UnblockInput()
    {
        _executionMessageState.Clear();
        _inputStateManager.SetProcessing(false);
        _sessionManager.AcquireSession(HandleBarcodeScanned);
    }

    private void HandleSequenceCompleted()
    {
        LogSequenceCompleted();
        UnblockInput();
        ShowSequenceCompletionNotification();
    }

    private void LogSequenceCompleted()
    {
        _logger.LogInformation("Последовательность завершена. Ошибки: {HasErrors}", _coordinator.HasErrors);
    }

    private void ShowSequenceCompletionNotification()
    {
        if (_coordinator.HasErrors)
        {
            _errorHandler.ShowError("Тест завершён", "Выполнение прервано из-за ошибки");
            return;
        }
        _errorHandler.ShowSuccess("Тест завершён", "Все шаги выполнены успешно");
    }

    public void Dispose()
    {
        _disposed = true;
        _sessionManager.ReleaseSession();
        UnsubscribeFromEvents();
    }

    private void UnsubscribeFromEvents()
    {
        _operatorState.OnChange -= HandleStateChange;
        _autoReady.OnChange -= HandleStateChange;
        _coordinator.OnSequenceCompleted -= HandleSequenceCompleted;
        if (_messageProviderKey != null)
        {
            _messageService.UnregisterProvider(_messageProviderKey);
        }
    }
}
 