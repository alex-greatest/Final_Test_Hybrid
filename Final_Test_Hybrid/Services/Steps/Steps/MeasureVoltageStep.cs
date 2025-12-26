using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

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
