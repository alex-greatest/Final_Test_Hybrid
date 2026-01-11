using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class InitializeRecipeProviderStep(
    IRecipeProvider recipeProvider,
    BoilerState boilerState,
    ILogger<InitializeRecipeProviderStep> logger,
    ITestStepLogger testStepLogger) : IPreExecutionStep
{
    public string Id => "initialize-recipe-provider";
    public string Name => "Инициализация рецептов";
    public string Description => "Загружает рецепты в провайдер";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        try
        {
            var recipes = boilerState.Recipes ?? [];
            recipeProvider.SetRecipes(recipes);
            LogInfo("Рецепты загружены: {Count}", recipes.Count);
            return Task.FromResult(PreExecutionResult.Continue());
        }
        catch (ArgumentException ex)
        {
            var error = $"Ошибка инициализации рецептов: {ex.Message}";
            LogError(ex, "{Error}", error);
            return Task.FromResult(PreExecutionResult.Fail(error));
        }
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    private void LogError(Exception ex, string message, params object?[] args)
    {
        logger.LogError(ex, message, args);
        testStepLogger.LogError(ex, message, args);
    }
}
