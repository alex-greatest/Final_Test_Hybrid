namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

public interface ITestStepRegistry
{
    IReadOnlyList<ITestStep> Steps { get; }
    IReadOnlyList<ITestStep> VisibleSteps { get; }
    ITestStep? GetById(string id);
    ITestStep? GetByName(string name);
}
