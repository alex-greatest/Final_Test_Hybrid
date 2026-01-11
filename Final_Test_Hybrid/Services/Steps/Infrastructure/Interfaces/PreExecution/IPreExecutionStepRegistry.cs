namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

public interface IPreExecutionStepRegistry
{
    IReadOnlyList<IPreExecutionStep> GetOrderedSteps();
}
