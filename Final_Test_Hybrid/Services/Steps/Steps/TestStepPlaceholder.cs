using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class TestStepPlaceholder : ITestStep
{
    public const string StepId = "<TEST STEP>";

    public string Id => StepId;
    public string Name => "<TEST STEP>";
    public string Description => "Заполнитель для строки";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Skip("Placeholder step"));
    }
}
