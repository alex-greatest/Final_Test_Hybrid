using System.Globalization;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class WriteRecipesToPlcStep(
    ExecutionMessageState messageState,
    DualLogger<WriteRecipesToPlcStep> logger) : IPreExecutionStep
{
    public string Id => "write-recipes-to-plc";
    public string Name => "Загрузка рецептов в PLC";
    public string Description => "Записывает рецепты в контроллер";
    public bool IsVisibleInStatusGrid => false;

    public async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var recipes = GetPlcRecipes(context);
        if (recipes.Count == 0)
        {
            return PreExecutionResult.Continue();
        }
        return await WriteAllRecipesAsync(recipes, context, ct);
    }

    private static List<RecipeResponseDto> GetPlcRecipes(PreExecutionContext context)
    {
        return context.BoilerState.Recipes?
            .Where(r => r.IsPlc)
            .ToList() ?? [];
    }

    private async Task<PreExecutionResult> WriteAllRecipesAsync(
        List<RecipeResponseDto> recipes,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var total = recipes.Count;
        for (var i = 0; i < total; i++)
        {
            UpdateProgress(i + 1, total);
            var result = await WriteRecipeAsync(recipes[i], context, ct);
            if (result.Status == PreExecutionStatus.Failed)
            {
                return result;
            }
        }
        return PreExecutionResult.Continue();
    }

    private void UpdateProgress(int current, int total)
    {
        messageState.SetMessage($"Загрузка рецептов ({current}/{total})...");
    }

    private async Task<PreExecutionResult> WriteRecipeAsync(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        logger.LogInformation("WriteRecipe: Tag={Tag}, Address=[{Address}], Value={Value}, Type={Type}",
            recipe.TagName, recipe.Address, recipe.Value, recipe.PlcType);
        var writeResult = await WriteValueByTypeAsync(recipe, context, ct);
        if (!writeResult.Success)
        {
            return HandleWriteError(recipe, writeResult.Error!);
        }
        return HandleWriteSuccess(recipe);
    }

    private PreExecutionResult HandleWriteSuccess(RecipeResponseDto recipe)
    {
        logger.LogInformation("Записан рецепт {Tag} = {Value}", recipe.TagName, recipe.Value);
        return PreExecutionResult.Continue();
    }

    private async Task<WriteResult> WriteValueByTypeAsync(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        return recipe.PlcType switch
        {
            PlcType.Real => await WriteFloatAsync(recipe, context, ct),
            PlcType.Int16 => await WriteInt16Async(recipe, context, ct),
            PlcType.Dint => await WriteInt32Async(recipe, context, ct),
            PlcType.Bool => await context.OpcUa.WriteAsync(
                recipe.Address, ParseBool(recipe.Value), ct),
            PlcType.String => await context.OpcUa.WriteAsync(
                recipe.Address, recipe.Value, ct),
            _ => new WriteResult(recipe.Address, $"Неизвестный тип: {recipe.PlcType}")
        };
    }

    private async Task<WriteResult> WriteFloatAsync(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        if (!TryParseFloat(recipe.Value, out var value))
        {
            return CreateParseError(recipe, "float");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
    }

    private async Task<WriteResult> WriteInt16Async(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        if (!short.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var value))
        {
            return CreateParseError(recipe, "Int16");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
    }

    private async Task<WriteResult> WriteInt32Async(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        if (!int.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var value))
        {
            return CreateParseError(recipe, "Dint");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
    }

    private static WriteResult CreateParseError(RecipeResponseDto recipe, string typeName)
    {
        return new WriteResult(recipe.Address, $"Некорректное значение '{recipe.Value}' для типа {typeName}");
    }

    private static bool TryParseFloat(string value, out float result)
    {
        return float.TryParse(value.Replace(',', '.'), CultureInfo.InvariantCulture, out result);
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.Ordinal);
    }

    private PreExecutionResult HandleWriteError(RecipeResponseDto recipe, string error)
    {
        var message = $"Ошибка записи {recipe.TagName}: {error}";
        logger.LogError("{Error}", message);
        var errorInfo = new RecipeWriteErrorInfo(recipe.TagName, recipe.Address, recipe.Value, error);
        return PreExecutionResult.Fail(message, new RecipeWriteErrorDetails([errorInfo]), "Ошибка загрузки рецептов");
    }
}
