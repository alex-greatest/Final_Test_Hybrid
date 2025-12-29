using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ScanStepManager : IDisposable
{
    private const int MessagePriority = 100;
    private readonly ScanSessionManager _sessionManager;
    private readonly ScanInputStateManager _inputStateManager;
    private readonly BarcodeProcessingPipeline _pipeline;
    private readonly ScanErrorHandler _errorHandler;
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly MessageService _messageService;
    private readonly TestExecutionCoordinator _coordinator;
    private readonly ITestStepLogger _testStepLogger;
    private readonly ILogger<ScanStepManager> _logger;
    public bool IsProcessing => _inputStateManager.IsProcessing;

    public event Action? OnChange
    {
        add => _inputStateManager.OnStateChanged += value;
        remove => _inputStateManager.OnStateChanged -= value;
    }

    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested
    {
        add => _errorHandler.OnMissingPlcTagsDialogRequested += value;
        remove => _errorHandler.OnMissingPlcTagsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested
    {
        add => _errorHandler.OnMissingRequiredTagsDialogRequested += value;
        remove => _errorHandler.OnMissingRequiredTagsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested
    {
        add => _errorHandler.OnUnknownStepsDialogRequested += value;
        remove => _errorHandler.OnUnknownStepsDialogRequested -= value;
    }

    public event Func<IReadOnlyList<MissingRecipeInfo>, Task>? OnMissingRecipesDialogRequested
    {
        add => _errorHandler.OnMissingRecipesDialogRequested += value;
        remove => _errorHandler.OnMissingRecipesDialogRequested -= value;
    }

    public ScanStepManager(
        ScanSessionManager sessionManager,
        ScanInputStateManager inputStateManager,
        BarcodeProcessingPipeline pipeline,
        ScanErrorHandler errorHandler,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        MessageService messageService,
        TestExecutionCoordinator coordinator,
        ITestStepLogger testStepLogger,
        ILogger<ScanStepManager> logger)
    {
        _sessionManager = sessionManager;
        _inputStateManager = inputStateManager;
        _pipeline = pipeline;
        _errorHandler = errorHandler;
        _operatorState = operatorState;
        _autoReady = autoReady;
        _messageService = messageService;
        _coordinator = coordinator;
        _testStepLogger = testStepLogger;
        _logger = logger;
        SubscribeToEvents();
        UpdateState();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnChange += HandleStateChange;
        _autoReady.OnChange += HandleStateChange;
        _messageService.RegisterProvider(MessagePriority, GetScanMessage);
        _coordinator.OnSequenceCompleted += HandleSequenceCompleted;
    }

    private bool IsScanModeEnabled =>
        _operatorState.IsAuthenticated && _autoReady.IsReady;

    private string? GetScanMessage()
    {
        return IsScanModeEnabled ? "Отсканируйте серийный номер котла" : null;
    }

    private void HandleStateChange()
    {
        UpdateState();
    }

    private void UpdateState()
    {
        if (IsScanModeEnabled)
        {
            ActivateScanMode();
        }
        else
        {
            DeactivateScanMode();
        }
        _messageService.NotifyChanged();
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
        var result = await _pipeline.ProcessAsync(barcode);
        await _errorHandler.HandleResultAsync(result);
        if (!result.IsSuccess)
        {
            UnblockInput();
        }
    }

    private void BlockInput()
    {
        _sessionManager.ReleaseSession();
        _inputStateManager.SetProcessing(true);
    }

    private void UnblockInput()
    {
        _inputStateManager.SetProcessing(false);
        _sessionManager.AcquireSession(HandleBarcodeScanned);
    }

    private void HandleSequenceCompleted()
    {
        _logger.LogInformation("Последовательность завершена. Ошибки: {HasErrors}", _coordinator.HasErrors);
        UnblockInput();
        if (_coordinator.HasErrors)
        {
            _errorHandler.ShowError("Тест завершён", "Выполнение прервано из-за ошибки");
            return;
        }
        _errorHandler.ShowSuccess("Тест завершён", "Все шаги выполнены успешно");
    }

    public void Dispose()
    {
        _sessionManager.ReleaseSession();
        UnsubscribeFromEvents();
    }

    private void UnsubscribeFromEvents()
    {
        _operatorState.OnChange -= HandleStateChange;
        _autoReady.OnChange -= HandleStateChange;
        _coordinator.OnSequenceCompleted -= HandleSequenceCompleted;
    }
}
