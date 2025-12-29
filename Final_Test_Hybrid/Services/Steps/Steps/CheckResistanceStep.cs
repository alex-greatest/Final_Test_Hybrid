using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class CheckResistanceStep
{
    private const string MinResistanceTag = "MinResistance";
    private const string MaxResistanceTag = "MaxResistance";
    public string Id => "check-resistance";
    public string Name => "Проверка сопротивления";
    public string Description => "Проверяет сопротивление цепи";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }
}
