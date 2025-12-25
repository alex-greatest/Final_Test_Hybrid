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
        return ValidateNotEmpty(recipes)
            ?? await ValidateTagsExistInPlc(recipes, ct);
    }

    private TagValidationResult? ValidateNotEmpty(IReadOnlyList<RecipeResponseDto> recipes)
    {
        if (recipes.Count != 0)
        {
            return null;
        }
        const string error = "Список рецептов пуст";
        logger.LogError(error);
        testStepLogger.LogError(null, error);
        return new TagValidationResult([], error);
    }

    private async Task<TagValidationResult> ValidateTagsExistInPlc(
        IReadOnlyList<RecipeResponseDto> recipes,
        CancellationToken ct)
    {
        var browseResult = await browseService.BrowseChildTagsAsync(DbRecipeNodeId, ct);
        if (!browseResult.Success)
        {
            return HandleBrowseError(browseResult.Error);
        }
        var missingTags = FindMissingTags(recipes, browseResult.Addresses);
        LogMissingTagsResult(missingTags);
        return new TagValidationResult(missingTags, null);
    }

    private TagValidationResult HandleBrowseError(string? error)
    {
        logger.LogError("Ошибка browse PLC: {Error}", error);
        testStepLogger.LogError(null, "Ошибка browse PLC: {Error}", error);
        return new TagValidationResult([], error);
    }

    private List<string> FindMissingTags(
        IReadOnlyList<RecipeResponseDto> recipes,
        IReadOnlyList<string> plcAddresses)
    {
        var addressSet = new HashSet<string>(plcAddresses, StringComparer.OrdinalIgnoreCase);
        return recipes
            .Where(r => !string.IsNullOrEmpty(r.Address) && !addressSet.Contains(r.Address))
            .Select(r => $"{r.TagName} ({r.Address})")
            .ToList();
    }

    private void LogMissingTagsResult(List<string> missingTags)
    {
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
    }
}
