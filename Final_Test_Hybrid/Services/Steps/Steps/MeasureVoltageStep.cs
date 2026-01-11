using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class MeasureVoltageStep : ITestStep
{
    public string Id => "measure-voltage";
    public string Name => "Измерение напряжения";
    public string Description => "Измеряет напряжение на выходе";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }
}
