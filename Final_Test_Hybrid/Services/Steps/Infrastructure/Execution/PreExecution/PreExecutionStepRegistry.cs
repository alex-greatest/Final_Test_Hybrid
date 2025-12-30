using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionStepRegistry : IPreExecutionStepRegistry
{
    private readonly List<IPreExecutionStep> _steps = [];

    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps() => _steps;

    public void Register(IPreExecutionStep step)
    {
        _steps.Add(step);
    }
}
