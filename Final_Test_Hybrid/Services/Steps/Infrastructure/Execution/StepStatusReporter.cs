using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class StepStatusReporter
{
    private readonly TestSequenseService _sequenseService;
    private readonly ScanBarcodeStep _scanBarcodeStep;
    private readonly ScanBarcodeMesStep _scanBarcodeMesStep;

    public StepStatusReporter(
        TestSequenseService sequenseService,
        AppSettingsService appSettings,
        ScanBarcodeStep scanBarcodeStep,
        ScanBarcodeMesStep scanBarcodeMesStep)
    {
        _sequenseService = sequenseService;
        _scanBarcodeStep = scanBarcodeStep;
        _scanBarcodeMesStep = scanBarcodeMesStep;
        appSettings.UseMesChanged += OnUseMesChanged;
    }

    private void OnUseMesChanged(bool useMes)
    {
        ScanStepBase step = useMes ? _scanBarcodeMesStep : _scanBarcodeStep;
        _sequenseService.MutateScanStep(step.Name, step.Description);
    }

    /// <summary>
    /// Сообщает о старте шага без предзаданных пределов.
    /// </summary>
    public Guid ReportStepStarted(ITestStep step)
    {
        return _sequenseService.AddStep(step);
    }

    /// <summary>
    /// Сообщает о старте шага с предзаданными пределами.
    /// </summary>
    /// <param name="step">Шаг теста.</param>
    /// <param name="limits">Предзаданные пределы для отображения в гриде.</param>
    public Guid ReportStepStarted(ITestStep step, string? limits)
    {
        return _sequenseService.AddStep(step, limits);
    }

    public Guid ReportStepStarted(IPreExecutionStep step)
    {
        return _sequenseService.AddStep(step.Name, step.Description);
    }

    public Guid ReportStepStarted(string name, string description)
    {
        return _sequenseService.AddStep(name, description);
    }

    public void ReportStepCompleted(Guid id, TestStepResult result)
    {
        var limits = result.OutputData?.GetValueOrDefault("Limits")?.ToString();
        if (result.Success)
        {
            ReportSuccess(id, result.Message, limits);
            return;
        }
        ReportError(id, result.Message, limits);
    }

    public void ReportRetry(Guid id)
    {
        _sequenseService.SetRunning(id);
    }
    
    public void ReportError(Guid id, string message, string? limits = null)
    {
        _sequenseService.SetError(id, message, limits);
    }

    public void ReportSuccess(Guid id, string message = "", string? limits = null)
    {
        _sequenseService.SetSuccess(id, message, limits);
    }

    public void ClearAll()
    {
        _sequenseService.ClearAll();
    }

    public void ClearAllExceptScan()
    {
        _sequenseService.ClearAllExceptScan();
    }

    public void UpdateScanStepStatus(TestStepStatus status, string message, string? limits = null)
    {
        _sequenseService.UpdateScanStep(status, message, limits);
    }

    public Guid EnsureScanStepExists(string name, string description)
    {
        return _sequenseService.EnsureScanStepExists(name, description);
    }
}
