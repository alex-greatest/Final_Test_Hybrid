using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ValidateRecipesStep(
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    ExecutionMessageState messageState,
    DualLogger<ValidateRecipesStep> logger) : IPreExecutionStep
{
    public string Id => "validate-recipes";
    public string Name => "Проверка рецептов";
    public string Description => "Проверяет наличие необходимых рецептов для шагов";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        messageState.SetMessage("Проверка рецептов...");
        if (context.Maps == null || context.Maps.Count == 0)
        {
            return Task.FromResult(PreExecutionResult.Continue());
        }
        var allSteps = ExtractAllSteps(context.Maps);
        var validation = recipeValidator.Validate(allSteps, boilerState.Recipes);
        if (!validation.IsValid)
        {
            var error = $"Отсутствуют рецепты: {validation.MissingRecipes.Count}";
            logger.LogWarning("{Error}", error);
            return Task.FromResult(PreExecutionResult.Fail(
                error,
                new MissingRecipesDetails(validation.MissingRecipes),
                "Ошибка проверки рецептов"));
        }
        logger.LogInformation("Проверка рецептов пройдена");
        return Task.FromResult(PreExecutionResult.Continue());
    }

    private static List<ITestStep> ExtractAllSteps(List<TestMap> maps)
    {
        return maps
            .SelectMany(m => m.Rows)
            .SelectMany(r => r.Steps)
            .Where(s => s != null)
            .Cast<ITestStep>()
            .Distinct()
            .ToList();
    }
}
