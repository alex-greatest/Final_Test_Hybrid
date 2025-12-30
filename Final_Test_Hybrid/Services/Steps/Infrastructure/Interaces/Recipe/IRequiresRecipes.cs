using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;

public interface IRequiresRecipes : ITestStep
{
    IReadOnlyList<string> RequiredRecipeAddresses { get; }
}
