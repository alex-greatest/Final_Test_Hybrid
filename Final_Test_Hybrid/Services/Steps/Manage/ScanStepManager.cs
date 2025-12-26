using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Manage;

public class ScanStepManager : IDisposable
{
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly AppSettingsService _appSettings;
    private readonly ITestStepRegistry _stepRegistry;
    private readonly TestSequenseService _sequenseService;
    private readonly MessageService _messageService;
    private readonly RawInputService _rawInputService;
    private readonly SequenceValidationState _validationState;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ScanStepManager> _logger;
    private readonly ITestStepLogger _testStepLogger;
    private readonly Lock _sessionLock = new();
    private readonly SemaphoreSlim _processLock = new(1, 1);
    private IDisposable? _scanSession;
    public bool IsProcessing { get; private set; }
    public event Action? OnChange;
    public event Func<IReadOnlyList<string>, Task>? OnMissingPlcTagsDialogRequested;
    public event Func<IReadOnlyList<string>, Task>? OnMissingRequiredTagsDialogRequested;
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";
    private const int MessagePriority = 100;

    public ScanStepManager(
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        AppSettingsService appSettings,
        ITestStepRegistry stepRegistry,
        TestSequenseService sequenseService,
        MessageService messageService,
        RawInputService rawInputService,
        SequenceValidationState validationState,
        INotificationService notificationService,
        ILogger<ScanStepManager> logger,
        ITestStepLogger testStepLogger)
    {
        _operatorState = operatorState;
        _autoReady = autoReady;
        _appSettings = appSettings;
        _stepRegistry = stepRegistry;
        _sequenseService = sequenseService;
        _messageService = messageService;
        _rawInputService = rawInputService;
        _validationState = validationState;
        _notificationService = notificationService;
        _logger = logger;
        _testStepLogger = testStepLogger;
        _operatorState.OnChange += UpdateState;
        _autoReady.OnChange += UpdateState;
        _appSettings.UseMesChanged += OnUseMesChanged;
        _messageService.RegisterProvider(MessagePriority, GetScanMessage);
        UpdateState();
    }

    private bool ShouldShowScanStep =>
        _operatorState.IsAuthenticated && _autoReady.IsReady;

    private string? GetScanMessage() =>
        ShouldShowScanStep ? "Отсканируйте серийный номер котла" : null;

    private void OnUseMesChanged(bool _) => UpdateState();

    private void UpdateState()
    {
        if (ShouldShowScanStep)
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
            _scanSession ??= _rawInputService.RequestScan(OnBarcodeScanned);
        }
    }
    
    private async void OnBarcodeScanned(string barcode)
    {
        await ProcessBarcodeAsync(barcode);
    }

    public async Task ProcessBarcodeAsync(string barcode)
    {
        if (!await _processLock.WaitAsync(0))
        {
            return;
        }
        try
        {
            BlockInput();
            var result = await ProcessBarcodeInternalAsync(barcode);
            if (!result.IsSuccess)
            {
                UnblockInput();
            }
        }
        finally
        {
            _processLock.Release();
        }
    }
    
    private void BlockInput()
    {
        ReleaseScanSession();
        IsProcessing = true;
        OnChange?.Invoke();
    }

    private void UnblockInput()
    {
        IsProcessing = false;
        AcquireScanSession();
        OnChange?.Invoke();
    }
    
    private async Task<StepResult> ProcessBarcodeInternalAsync(string barcode)
    {
        _validationState.ClearError();
        try
        {
            var step = (IScanBarcodeStep)_stepRegistry.GetById(GetCurrentStepId())!;
            var result = await step.ProcessBarcodeAsync(barcode);
            if (result.IsSuccess)
            {
                return StepResult.Pass();
            }
            await ShowMissingPlcTagsDialogIfNeeded(result.MissingPlcTags);
            await ShowMissingRequiredTagsDialogIfNeeded(result.MissingRequiredTags);
            HandleStepError(result.ErrorMessage!);
            return StepResult.Fail(result.ErrorMessage!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка сканера: {Barcode}", barcode);
            HandleStepError("Ошибка сканера");
            return StepResult.WithError("Ошибка сканера");
        }
    }
    
    private async Task ShowMissingPlcTagsDialogIfNeeded(IReadOnlyList<string> missingTags)
    {
        if (missingTags.Count == 0)
        {
            return;
        }
        _notificationService.ShowWarning("Внимание", $"Обнаружено {missingTags.Count} отсутствующих тегов для PLC");
        if (OnMissingPlcTagsDialogRequested != null)
        {
            await OnMissingPlcTagsDialogRequested.Invoke(missingTags);
        }
    }

    private async Task ShowMissingRequiredTagsDialogIfNeeded(IReadOnlyList<string> missingTags)
    {
        if (missingTags.Count == 0)
        {
            return;
        }
        _notificationService.ShowWarning("Внимание", $"Обнаружено {missingTags.Count} обязательных тегов, отсутствующих в рецептах");
        if (OnMissingRequiredTagsDialogRequested != null)
        {
            await OnMissingRequiredTagsDialogRequested.Invoke(missingTags);
        }
    }

    private void HandleStepError(string error)
    {
        _validationState.SetError(error);
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
        _operatorState.OnChange -= UpdateState;
        _autoReady.OnChange -= UpdateState;
        _appSettings.UseMesChanged -= OnUseMesChanged;
    }
}
