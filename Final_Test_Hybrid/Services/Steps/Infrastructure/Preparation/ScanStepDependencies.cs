using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Preparation;

/// <summary>
/// Контейнер базовых зависимостей для шагов сканирования.
/// </summary>
public class ScanStepDependencies(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ExecutionMessageState messageState)
{
    public BarcodeScanService BarcodeScanService => barcodeScanService;
    public ITestSequenceLoader SequenceLoader => sequenceLoader;
    public ITestMapBuilder MapBuilder => mapBuilder;
    public ITestMapResolver MapResolver => mapResolver;
    public RecipeValidator RecipeValidator => recipeValidator;
    public BoilerState BoilerState => boilerState;
    public PausableOpcUaTagService OpcUa => opcUa;
    public IRecipeProvider RecipeProvider => recipeProvider;
    public ExecutionMessageState MessageState => messageState;
}
