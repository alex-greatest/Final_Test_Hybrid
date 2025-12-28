using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class ScanStepManager : IDisposable
{
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";
    private const int MessagePriority = 100;
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly AppSettingsService _appSettings;
    private readonly ITestStepRegistry _stepRegistry;
    private readonly ITestMapResolver _mapResolver;
    private readonly TestSequenseService _sequenseService;
    private readonly MessageService _messageService;
    private readonly RawInputService _rawInputService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ScanStepManager> _logger;
    private readonly ITestStepLogger _testStepLogger;
    private readonly TestExecutionCoordinator _coordinator;
    private readonly Lock _sessionLock = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private IDisposable? _scanSession;

    public bool IsProcessing { get; private set; }
    public event Action? OnChange;
    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested;
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested;
    public event Func<IReadOnlyList<UnknownStepInfo>, Task>? OnUnknownStepsDialogRequested;

    public ScanStepManager(
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        AppSettingsService appSettings,
        ITestStepRegistry stepRegistry,
        ITestMapResolver mapResolver,
        TestSequenseService sequenseService,
        MessageService messageService,
        RawInputService rawInputService,
        INotificationService notificationService,
        ILogger<ScanStepManager> logger,
        ITestStepLogger testStepLogger,
        TestExecutionCoordinator coordinator)
    {
        _operatorState = operatorState;
        _autoReady = autoReady;
        _appSettings = appSettings;
        _stepRegistry = stepRegistry;
        _mapResolver = mapResolver;
        _sequenseService = sequenseService;
        _messageService = messageService;
        _rawInputService = rawInputService;
        _notificationService = notificationService;
        _logger = logger;
        _testStepLogger = testStepLogger;
        _coordinator = coordinator;
        SubscribeToEvents();
        UpdateState();
    }

    private void SubscribeToEvents()
    {
        _operatorState.OnChange += HandleStateChange;
        _autoReady.OnChange += HandleStateChange;
        _appSettings.UseMesChanged += HandleUseMesChanged;
        _messageService.RegisterProvider(MessagePriority, GetScanMessage);
        _coordinator.OnSequenceCompleted += HandleSequenceCompleted;
    }

    private bool IsScanModeEnabled =>
        _operatorState.IsAuthenticated && _autoReady.IsReady;

    private string? GetScanMessage() =>
        IsScanModeEnabled ? "Отсканируйте серийный номер котла" : null;

    private void HandleUseMesChanged(bool _) =>
        UpdateState();

    private void HandleStateChange() =>
        UpdateState();

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
        AcquireScanSession();
        InitializeCurrentStep();
    }

    private void InitializeCurrentStep()
    {
        var step = _stepRegistry.GetById(GetCurrentStepId());
        _testStepLogger.StartNewSession();
        _sequenseService.SetCurrentStep(step);
    }

    private void DeactivateScanMode()
    {
        ReleaseScanSession();
        _sequenseService.ClearCurrentStep();
    }

    private string GetCurrentStepId() =>
        _appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;

    private void AcquireScanSession()
    {
        lock (_sessionLock)
        {
            _scanSession ??= _rawInputService.RequestScan(HandleBarcodeScanned);
        }
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
        if (!TryAcquireProcessLock())
        {
            return;
        }
        try
        {
            await ExecuteBarcodeProcessing(barcode);
        }
        finally
        {
            _processLock.Release();
        }
    }

    private bool TryAcquireProcessLock() =>
        _processLock.Wait(0);

    private async Task ExecuteBarcodeProcessing(string barcode)
    {
        BlockInput();
        var result = await ProcessBarcodeWithErrorHandling(barcode);
        if (!result.IsSuccess)
        {
            UnblockInput();
        }
    }

    private void BlockInput()
    {
        ReleaseScanSession();
        IsProcessing = true;
        NotifyStateChanged();
    }

    private void UnblockInput()
    {
        IsProcessing = false;
        AcquireScanSession();
        NotifyStateChanged();
    }

    private void NotifyStateChanged() =>
        OnChange?.Invoke();

    private async Task<StepResult> ProcessBarcodeWithErrorHandling(string barcode)
    {
        try
        {
            return await ProcessBarcodeCore(barcode);
        }
        catch (Exception ex)
        {
            return HandleScannerError(ex, barcode);
        }
    }

    private async Task<StepResult> ProcessBarcodeCore(string barcode)
    {
        var step = GetCurrentScanStep();
        var result = await step.ProcessBarcodeAsync(barcode);
        if (!result.IsSuccess)
        {
            return await HandleBarcodeFailure(result);
        }
        return ResolveAndValidateMaps(result.RawMaps!);
    }

    private IScanBarcodeStep GetCurrentScanStep() =>
        (IScanBarcodeStep)_stepRegistry.GetById(GetCurrentStepId())!;

    private StepResult HandleScannerError(Exception ex, string barcode)
    {
        _logger.LogError(ex, "Ошибка сканера: {Barcode}", barcode);
        ReportError("Ошибка сканера");
        return StepResult.WithError("Ошибка сканера");
    }

    private async Task<StepResult> HandleBarcodeFailure(BarcodeStepResult result)
    {
        await ShowMissingPlcTagsDialog(result.MissingPlcTags);
        await ShowMissingRequiredTagsDialog(result.MissingRequiredTags);
        ReportError(result.ErrorMessage!);
        return StepResult.Fail(result.ErrorMessage!);
    }

    private StepResult ResolveAndValidateMaps(List<RawTestMap> rawMaps)
    {
        var resolveResult = _mapResolver.Resolve(rawMaps);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            return HandleUnknownSteps(resolveResult.UnknownSteps);
        }
        StartExecution(resolveResult.Maps!);
        return StepResult.Pass();
    }

    private void StartExecution(List<TestMap> maps)
    {
        _logger.LogInformation("Запуск выполнения {Count} maps", maps.Count);
        _sequenseService.SetSuccessOnCurrent();
        _coordinator.SetMaps(maps);
        _ = _coordinator.StartAsync();
    }

    private void HandleSequenceCompleted()
    {
        _logger.LogInformation("Последовательность завершена. Ошибки: {HasErrors}", _coordinator.HasErrors);
        UnblockInput();
        if (_coordinator.HasErrors)
        {
            _notificationService.ShowError("Тест завершён", "Выполнение прервано из-за ошибки");
            return;
        }
        _notificationService.ShowSuccess("Тест завершён", "Все шаги выполнены успешно");
    }

    private StepResult HandleUnknownSteps(IReadOnlyList<UnknownStepInfo> unknownSteps)
    {
        var error = $"Неизвестных шагов: {unknownSteps.Count}";
        NotifyUnknownStepsDetected(unknownSteps.Count);
        _ = InvokeDialogHandler(OnUnknownStepsDialogRequested, unknownSteps);
        ReportError(error);
        return StepResult.Fail(error);
    }

    private void NotifyUnknownStepsDetected(int count) =>
        _notificationService.ShowWarning(
            "Внимание",
            $"Обнаружено {count} неизвестных шагов в последовательности");

    private async Task ShowMissingPlcTagsDialog(IReadOnlyList<string> missingTags) =>
        await ShowTagsDialog(
            missingTags,
            count => $"Обнаружено {count} отсутствующих тегов для PLC",
            OnMissingPlcTagsDialogRequested);

    private async Task ShowMissingRequiredTagsDialog(IReadOnlyList<string> missingTags) =>
        await ShowTagsDialog(
            missingTags,
            count => $"Обнаружено {count} обязательных тегов, отсутствующих в рецептах",
            OnMissingRequiredTagsDialogRequested);

    private async Task ShowTagsDialog(
        IReadOnlyList<string> tags,
        Func<int, string> messageBuilder,
        Func<IReadOnlyList<string>, Task>? dialogHandler)
    {
        if (tags.Count == 0)
        {
            return;
        }
        _notificationService.ShowWarning("Внимание", messageBuilder(tags.Count));
        await InvokeDialogHandler(dialogHandler, tags);
    }

    private static async Task InvokeDialogHandler<T>(
        Func<IReadOnlyList<T>, Task>? handler,
        IReadOnlyList<T> items)
    {
        if (handler != null)
        {
            await handler.Invoke(items);
        }
    }

    private void ReportError(string error)
    {
        _notificationService.ShowError("Ошибка", error);
        _sequenseService.SetErrorOnCurrent(error);
    }

    private void ReleaseScanSession()
    {
        lock (_sessionLock)
        {
            _scanSession?.Dispose();
            _scanSession = null;
        }
    }

    public void Dispose()
    {
        ReleaseScanSession();
        UnsubscribeFromEvents();
        _processLock.Dispose();
    }

    private void UnsubscribeFromEvents()
    {
        _operatorState.OnChange -= HandleStateChange;
        _autoReady.OnChange -= HandleStateChange;
        _appSettings.UseMesChanged -= HandleUseMesChanged;
        _coordinator.OnSequenceCompleted -= HandleSequenceCompleted;
    }
}
