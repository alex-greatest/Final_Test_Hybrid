using Final_Test_Hybrid.Services.Steps.Infrastructure;

namespace Final_Test_Hybrid.Services.Steps.Interaces;

public interface ITestStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    bool IsVisibleInEditor => true;
    Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct);
}
