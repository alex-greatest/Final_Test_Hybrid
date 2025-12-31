using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class StepStatusReporter
{
    private readonly TestSequenseService _sequenseService;

    public StepStatusReporter(TestSequenseService sequenseService, AppSettingsService appSettings)
    {
        _sequenseService = sequenseService;
        appSettings.UseMesChanged += _ => ClearAll();
    }

    public Guid ReportStepStarted(ITestStep step)
    {
        return _sequenseService.AddStep(step);
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

    /// <summary>
    /// Skipped steps retain their error status - no status change needed.
    /// </summary>
    public void ReportSkip(Guid id)
    {
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
}
