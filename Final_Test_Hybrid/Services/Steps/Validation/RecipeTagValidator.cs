using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public record TagValidationResult(IReadOnlyList<string> MissingTags, string? BrowseError)
{
    public bool Success => BrowseError == null && MissingTags.Count == 0;
    public string? ErrorMessage => BrowseError ?? (MissingTags.Count > 0
        ? $"Отсутствуют теги в PLC: {MissingTags.Count}"
        : null);
}

public class RecipeTagValidator(
    OpcUaBrowseService browseService,
    ILogger<RecipeTagValidator> logger,
    ITestStepLogger testStepLogger)
{
    private const string DbRecipeNodeId = "ns=3;s=\"DB_Recipe\"";

    public async Task<TagValidationResult> ValidateAsync(
        IReadOnlyList<RecipeResponseDto> recipes,
        CancellationToken ct = default)
    {
        if (recipes.Count == 0)
        {
            const string error = "Список рецептов пуст";
            logger.LogError(error);
            testStepLogger.LogError(null, error);
            return new TagValidationResult([], error);
        }
        var plcRecipes = recipes.Where(r => r.IsPlc).ToList();
        if (plcRecipes.Count == 0)
        {
            logger.LogInformation("Нет рецептов для PLC, валидация пропущена");
            return new TagValidationResult([], null);
        }
        var browseResult = await browseService.BrowseChildTagsAsync(DbRecipeNodeId, ct);
        if (!browseResult.Success)
        {
            logger.LogError("Ошибка browse PLC: {Error}", browseResult.Error);
            testStepLogger.LogError(null, "Ошибка browse PLC: {Error}", browseResult.Error);
            return new TagValidationResult([], browseResult.Error);
        }
        var plcAddresses = new HashSet<string>(browseResult.Addresses, StringComparer.OrdinalIgnoreCase);
        var missingTags = plcRecipes
            .Where(r => !string.IsNullOrEmpty(r.Address) && !plcAddresses.Contains(r.Address))
            .Select(r => $"{r.TagName} ({r.Address})")
            .ToList();
        if (missingTags.Count > 0)
        {
            logger.LogWarning("Отсутствующие теги в PLC: {Tags}", string.Join(", ", missingTags));
            testStepLogger.LogWarning("Отсутствующие теги в PLC: {Tags}", string.Join(", ", missingTags));
        }
        else
        {
            logger.LogInformation("Все теги рецептов найдены в PLC");
            testStepLogger.LogInformation("Все теги рецептов найдены в PLC");
        }
        return new TagValidationResult(missingTags, null);
    }
}
