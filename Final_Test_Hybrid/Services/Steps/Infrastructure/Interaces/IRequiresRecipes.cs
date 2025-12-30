namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

public interface IRequiresRecipes : ITestStep
{
    IReadOnlyList<string> RequiredRecipeAddresses { get; }
}
