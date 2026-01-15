using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Тестовый шаг, который всегда возвращает ошибку.
/// Использовать для проверки логики обработки ошибок.
/// </summary>
public class AlwaysFailStep1 : ITestStep
{
    public string Id => "AlwaysFailStep1";
    public string Name => "AlwaysFailStep1";
    public string Description => "Тестовый шаг (всегда ошибка)";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Fail("Тестовая ошибка для проверки"));
    }
}
