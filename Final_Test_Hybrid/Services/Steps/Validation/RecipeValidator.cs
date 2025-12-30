using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public record MissingRecipeInfo(string StepName, string RecipeAddress);

public record RecipeValidationResult(
    bool IsValid,
    IReadOnlyList<MissingRecipeInfo> MissingRecipes);

public class RecipeValidator(
    ILogger<RecipeValidator> logger,
    ITestStepLogger testStepLogger)
{
    public RecipeValidationResult Validate(
        IReadOnlyList<ITestStep> steps,
        IReadOnlyList<RecipeResponseDto>? recipes)
    {
        var recipeAddresses = BuildRecipeAddressSet(recipes);
        var missingRecipes = FindMissingRecipes(steps, recipeAddresses);
        LogResult(missingRecipes);
        return new RecipeValidationResult(missingRecipes.Count == 0, missingRecipes);
    }

    private static HashSet<string> BuildRecipeAddressSet(IReadOnlyList<RecipeResponseDto>? recipes)
    {
        if (recipes == null || recipes.Count == 0)
        {
            return [];
        }
        return recipes.Select(r => r.Address).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<MissingRecipeInfo> FindMissingRecipes(
        IReadOnlyList<ITestStep> steps,
        HashSet<string> recipeAddresses)
    {
        return steps
            .OfType<IRequiresRecipes>()
            .SelectMany(step => FindMissingForStep(step, recipeAddresses))
            .ToList();
    }

    private static IEnumerable<MissingRecipeInfo> FindMissingForStep(
        IRequiresRecipes step,
        HashSet<string> recipeAddresses)
    {
        return step.RequiredRecipeAddresses
            .Where(address => !recipeAddresses.Contains(address))
            .Select(address => new MissingRecipeInfo(step.Name, address));
    }

    private void LogResult(List<MissingRecipeInfo> missingRecipes)
    {
        if (missingRecipes.Count == 0)
        {
            LogInfo("Все требуемые рецепты найдены");
            return;
        }
        LogMissingRecipes(missingRecipes);
    }

    private void LogMissingRecipes(List<MissingRecipeInfo> missingRecipes)
    {
        LogWarning("Отсутствуют рецепты: {Count}", missingRecipes.Count);
        foreach (var missing in missingRecipes)
        {
            LogWarning("  Шаг '{StepName}' требует рецепт '{Address}'",
                missing.StepName, missing.RecipeAddress);
        }
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    private void LogWarning(string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        testStepLogger.LogWarning(message, args);
    }
}
