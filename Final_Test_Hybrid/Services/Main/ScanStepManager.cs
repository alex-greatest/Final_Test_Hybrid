using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps;

namespace Final_Test_Hybrid.Services.Main;

public class ScanStepManager : IDisposable
{
    private readonly OperatorState _operatorState;
    private readonly AutoReadySubscription _autoReady;
    private readonly AppSettingsService _appSettings;
    private readonly ITestStepRegistry _stepRegistry;
    private readonly TestSequenseService _sequenseService;
    private readonly MessageService _messageService;
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";
    private const int MessagePriority = 100;

    public ScanStepManager(
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        AppSettingsService appSettings,
        ITestStepRegistry stepRegistry,
        TestSequenseService sequenseService,
        MessageService messageService)
    {
        _operatorState = operatorState;
        _autoReady = autoReady;
        _appSettings = appSettings;
        _stepRegistry = stepRegistry;
        _sequenseService = sequenseService;
        _messageService = messageService;
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
            _sequenseService.ClearCurrentStep();
            _messageService.NotifyChanged();
            return;
        }

        var stepId = _appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
        var step = _stepRegistry.GetById(stepId);
        _sequenseService.SetCurrentStep(step);
        _messageService.NotifyChanged();
    }

    public void Dispose()
    {
        _operatorState.OnChange -= UpdateState;
        _autoReady.OnChange -= UpdateState;
        _appSettings.UseMesChanged -= OnUseMesChanged;
    }
}
