namespace Final_Test_Hybrid.Services.Steps;

public interface ITestStepRegistry
{
    IReadOnlyList<ITestStep> Steps { get; }
    ITestStep? GetById(string id);
}
