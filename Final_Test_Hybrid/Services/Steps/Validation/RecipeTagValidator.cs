using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public record TagValidationResult(
    IReadOnlyList<string> MissingInPlc,
    IReadOnlyList<string> MissingInRecipes,
    string? BrowseError)
{
    public bool Success => BrowseError == null && MissingInPlc.Count == 0 && MissingInRecipes.Count == 0;
    public string? ErrorMessage => BrowseError ?? BuildMissingTagsMessage();
    public IReadOnlyList<string> MissingTags => MissingInPlc.Concat(MissingInRecipes).ToList();

    private string? BuildMissingTagsMessage()
    {
        var parts = new[]
        {
            MissingInPlc.Count > 0 ? $"В PLC отсутствуют теги из БД: {MissingInPlc.Count}" : null,
            MissingInRecipes.Count > 0 ? $"В БД отсутствуют теги из PLC: {MissingInRecipes.Count}" : null
        }.Where(p => p != null);
        return parts.Any() ? string.Join("; ", parts) : null;
    }
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
        LogError(error);
        return new TagValidationResult([], [], error);
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
        var missingInPlc = FindMissingInPlc(recipes, browseResult.Addresses);
        var missingInRecipes = FindMissingInRecipes(recipes, browseResult.Addresses);
        LogValidationResult(missingInPlc, missingInRecipes);
        return new TagValidationResult(missingInPlc, missingInRecipes, null);
    }

    private TagValidationResult HandleBrowseError(string? error)
    {
        LogError("Ошибка browse PLC: {Error}", error);
        return new TagValidationResult([], [], error);
    }

    private List<string> FindMissingInPlc(
        IReadOnlyList<RecipeResponseDto> recipes,
        IReadOnlyList<string> plcAddresses)
    {
        var plcAddressSet = new HashSet<string>(plcAddresses, StringComparer.OrdinalIgnoreCase);
        return recipes
            .Where(r => !string.IsNullOrEmpty(r.Address) && !plcAddressSet.Contains(r.Address))
            .Select(r => $"{r.TagName} ({r.Address})")
            .ToList();
    }

    private List<string> FindMissingInRecipes(
        IReadOnlyList<RecipeResponseDto> recipes,
        IReadOnlyList<string> plcAddresses)
    {
        var recipeAddressSet = new HashSet<string>(
            recipes.Where(r => !string.IsNullOrEmpty(r.Address)).Select(r => r.Address),
            StringComparer.OrdinalIgnoreCase);
        return plcAddresses
            .Where(addr => !recipeAddressSet.Contains(addr))
            .ToList();
    }

    private void LogValidationResult(List<string> missingInPlc, List<string> missingInRecipes)
    {
        LogMissingInPlc(missingInPlc);
        LogMissingInRecipes(missingInRecipes);
        LogSuccessIfAllSynced(missingInPlc, missingInRecipes);
    }

    private void LogMissingInPlc(List<string> missing)
    {
        if (missing.Count == 0)
        {
            return;
        }
        LogWarning("В PLC отсутствуют теги из БД: {Tags}", string.Join(", ", missing));
    }

    private void LogMissingInRecipes(List<string> missing)
    {
        if (missing.Count == 0)
        {
            return;
        }
        LogWarning("В БД отсутствуют теги из PLC: {Tags}", string.Join(", ", missing));
    }

    private void LogSuccessIfAllSynced(List<string> missingInPlc, List<string> missingInRecipes)
    {
        if (missingInPlc.Count != 0 || missingInRecipes.Count != 0)
        {
            return;
        }
        LogInfo("Все теги синхронизированы между PLC и БД");
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
