using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class PrintLabelStep : ITestStep
{
    public string Id => "print-label";
    public string Name => "Печать этикетки";
    public string Description => "Печатает этикетку на принтере";

    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        return TestStepResult.Pass();
    }
}
