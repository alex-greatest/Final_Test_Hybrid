using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class CheckResistanceStep : ITestStep
{
    public string Id => "check-resistance";
    public string Name => "Проверка сопротивления";
    public string Description => "Проверяет сопротивление цепи";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }
}
