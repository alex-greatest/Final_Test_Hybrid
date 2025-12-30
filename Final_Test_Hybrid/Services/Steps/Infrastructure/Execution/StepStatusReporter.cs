using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;

public class StepStatusReporter(TestSequenseService sequenseService)
{
    private const string DefaultErrorMessage = "Ошибка";

    public Guid ReportStepStarted(ITestStep step)
    {
        return sequenseService.AddStep(step);
    }

    public Guid ReportStepStarted(string name, string description)
    {
        return sequenseService.AddStep(name, description);
    }

    public void ReportStepCompleted(Guid id, TestStepResult result)
    {
        if (result.Success)
        {
            ReportSuccess(id, result.Message);
            return;
        }

        ReportError(id, result.Message);
    }

    public void ReportRetry(Guid id)
    {
        sequenseService.SetRunning(id);
    }

    /// <summary>
    /// Skipped steps retain their error status - no status change needed.
    /// </summary>
    public void ReportSkip(Guid id)
    {
    }

    public void ReportError(Guid id, string message)
    {
        sequenseService.SetError(id, message);
    }

    public void ReportSuccess(Guid id, string message = "")
    {
        sequenseService.SetSuccess(id, message);
    }
}
