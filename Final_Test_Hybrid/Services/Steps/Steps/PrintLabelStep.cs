using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class PrintLabelStep : ITestStep
{
    public string Id => "print-label";
    public string Name => "Печать этикетки";
    public string Description => "Печатает этикетку на принтере";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }
}
