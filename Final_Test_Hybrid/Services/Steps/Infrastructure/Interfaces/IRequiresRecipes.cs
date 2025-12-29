using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces;

public interface IRequiresRecipes : ITestStep
{
    IReadOnlyList<string> RequiredRecipeAddresses { get; }
}
