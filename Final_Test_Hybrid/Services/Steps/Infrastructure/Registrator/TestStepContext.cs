using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

public class TestStepContext(
    int columnIndex,
    OpcUaTagService opcUa,
    ILogger logger,
    IRecipeProvider recipeProvider)
{
    public int ColumnIndex { get; } = columnIndex;
    public OpcUaTagService OpcUa { get; } = opcUa;
    public ILogger Logger { get; } = logger;
    public IRecipeProvider RecipeProvider { get; } = recipeProvider;
    public Dictionary<string, object> Variables { get; } = [];
}
