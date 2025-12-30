namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

public interface IPreExecutionStepRegistry
{
    IReadOnlyList<IPreExecutionStep> GetOrderedSteps();
    void Register(IPreExecutionStep step);
}
