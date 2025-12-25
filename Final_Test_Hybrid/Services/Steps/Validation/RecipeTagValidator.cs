using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public record TagValidationResult(IReadOnlyList<string> MissingTags, string? BrowseError)
{
    public bool Success => BrowseError == null && MissingTags.Count == 0;
    public string? ErrorMessage => BrowseError ?? (MissingTags.Count > 0
        ? $"В БД отсутствуют теги из PLC: {MissingTags.Count}"
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
            ?? await ValidateTagsAsync(recipes, ct);
    }

    private TagValidationResult? ValidateNotEmpty(IReadOnlyList<RecipeResponseDto> recipes)
    {
        if (recipes.Count != 0)
        {
            return null;
        }
        const string error = "Список рецептов пуст";
        LogError(error);
        return new TagValidationResult([], error);
    }

    private async Task<TagValidationResult> ValidateTagsAsync(
        IReadOnlyList<RecipeResponseDto> recipes,
        CancellationToken ct)
    {
        var browseResult = await browseService.BrowseAllTagsRecursiveAsync(DbRecipeNodeId, ct);
        if (!browseResult.Success)
        {
            return HandleBrowseError(browseResult.Error);
        }
        var missingTags = FindMissingTags(recipes, browseResult.Addresses);
        LogResult(missingTags);
        return new TagValidationResult(missingTags, null);
    }

    private TagValidationResult HandleBrowseError(string? error)
    {
        LogError("Ошибка browse PLC: {Error}", error);
        return new TagValidationResult([], error);
    }

    private List<string> FindMissingTags(
        IReadOnlyList<RecipeResponseDto> recipes,
        IReadOnlyList<string> plcAddresses)
    {
        var recipeAddressSet = new HashSet<string>(
            recipes.Where(r => !string.IsNullOrEmpty(r.Address)).Select(r => r.Address),
            StringComparer.OrdinalIgnoreCase);
        return plcAddresses
            .Where(addr => addr.Contains("DB_Recipe", StringComparison.OrdinalIgnoreCase))
            .Where(addr => !recipeAddressSet.Contains(addr))
            .ToList();
    }

    private void LogResult(List<string> missingTags)
    {
        if (missingTags.Count > 0)
        {
            LogWarning("В БД отсутствуют теги из PLC: {Tags}", string.Join(", ", missingTags));
            return;
        }
        LogInfo("Все теги PLC найдены в БД рецептов");
    }

    private void LogError(string message, params object?[] args)
    {
        logger.LogError(message, args);
        testStepLogger.LogError(null, message, args);
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
