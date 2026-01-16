using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Тестовый шаг, который всегда возвращает ошибку.
/// Использовать для проверки логики обработки ошибок.
/// </summary>
public class AlwaysFailStep2 : ITestStep
{
    public string Id => "AlwaysFailStep2";
    public string Name => "AlwaysFailStep2";
    public string Description => "Тестовый шаг (всегда ошибка)";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var key = $"{Id}_failed";
        if (context.Variables.ContainsKey(key))
        {
            return Task.FromResult(TestStepResult.Pass());
        }

        context.Variables[key] = true;
        return Task.FromResult(TestStepResult.Fail("Тестовая ошибка для проверки"));
    }
}
