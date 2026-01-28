using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

public interface ITestStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsVisibleInEditor => true;

    /// <summary>
    /// Источник ошибки для отображения в UI (без скобок).
    /// </summary>
    string ErrorSourceTitle => ErrorSourceDefaults.Stand;

    Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct);
}
