using Final_Test_Hybrid.Models.Validation;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public class RequiredTagValidator(
    ILogger<RequiredTagValidator> logger,
    ITestStepLogger testStepLogger)
{
    public TagValidationResult Validate(IReadOnlyList<RecipeResponseDto> recipes)
    {
        if (RequiredRecipeTags.All.Count == 0)
        {
            return new TagValidationResult([], null);
        }
        var missingTags = FindMissingTags(recipes);
        LogResult(missingTags);
        return new TagValidationResult(missingTags, null);
    }

    private static List<string> FindMissingTags(IReadOnlyList<RecipeResponseDto> recipes)
    {
        var recipeAddresses = new HashSet<string>(
            recipes.Where(r => !string.IsNullOrEmpty(r.Address)).Select(r => r.Address),
            StringComparer.OrdinalIgnoreCase);
        return RequiredRecipeTags.All
            .Where(tag => !recipeAddresses.Contains(tag))
            .ToList();
    }

    private void LogResult(List<string> missingTags)
    {
        if (missingTags.Count > 0)
        {
            LogWarning("В рецептах БД отсутствуют обязательные теги: {Tags}", string.Join(", ", missingTags));
            return;
        }
        LogInfo("Все обязательные теги найдены в рецептах БД");
    }

    private void LogWarning(string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        testStepLogger.LogWarning(message, args);
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }
}
