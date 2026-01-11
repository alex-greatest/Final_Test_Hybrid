using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;

public interface IRequiresRecipes : ITestStep
{
    IReadOnlyList<string> RequiredRecipeAddresses { get; }
}
