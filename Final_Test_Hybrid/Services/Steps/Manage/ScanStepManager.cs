using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Interaces;

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
    private readonly BarcodeScanService _barcodeScanService;
    private readonly Lock _sessionLock = new();
    private IDisposable? _scanSession;
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
        BarcodeScanService barcodeScanService)
    {
        _operatorState = operatorState;
        _autoReady = autoReady;
        _appSettings = appSettings;
        _stepRegistry = stepRegistry;
        _sequenseService = sequenseService;
        _messageService = messageService;
        _rawInputService = rawInputService;
        _barcodeScanService = barcodeScanService;
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
        if (!ShouldShowScanStep)
        {
            ReleaseScanSession();
            _sequenseService.ClearCurrentStep();
            _messageService.NotifyChanged();
            return;
        }
        AcquireScanSession();
        var stepId = _appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
        var step = _stepRegistry.GetById(stepId);
        _sequenseService.SetCurrentStep(step);
        _messageService.NotifyChanged();
    }

    private void AcquireScanSession()
    {
        lock (_sessionLock)
        {
            _scanSession ??= _rawInputService.RequestScan(OnBarcodeScanned);
        }
    }
    
    private void OnBarcodeScanned(string barcode)
    {
        _barcodeScanService.ProcessBarcode(barcode);
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
