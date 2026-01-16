using System.Globalization;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Базовый класс для шагов сканирования (Non-MES и MES).
/// Содержит общую логику подготовки к тесту.
/// </summary>
public abstract class ScanStepBase(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ExecutionPhaseState phaseState)
    : IPreExecutionStep
{
    protected readonly BarcodeScanService BarcodeScanService = barcodeScanService;
    protected readonly ITestSequenceLoader SequenceLoader = sequenceLoader;
    protected readonly ITestMapBuilder MapBuilder = mapBuilder;
    protected readonly ITestMapResolver MapResolver = mapResolver;
    protected readonly RecipeValidator RecipeValidator = recipeValidator;
    protected readonly BoilerState BoilerState = boilerState;
    protected readonly PausableOpcUaTagService OpcUa = opcUa;
    protected readonly IRecipeProvider RecipeProvider = recipeProvider;
    protected readonly ExecutionPhaseState PhaseState = phaseState;

    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Description { get; }
    public bool IsVisibleInStatusGrid => true;
    public bool IsSkippable => true;

    public abstract Task<PreExecutionResult> ExecuteAsync(
        PreExecutionContext context,
        CancellationToken ct);

    protected abstract IDualLogger Logger { get; }

    #region Валидация баркода

    protected PreExecutionResult? ValidateBarcode(PreExecutionContext context)
    {
        var validation = BarcodeScanService.Validate(context.Barcode);
        if (!validation.IsValid)
        {
            return PreExecutionResult.Fail(validation.Error!);
        }
        context.BarcodeValidation = validation;
        Logger.LogInformation("Штрихкод валиден: {Barcode}, артикул: {Article}",
            validation.Barcode, validation.Article);
        return null;
    }

    #endregion

    #region Загрузка и построение maps

    protected async Task<PreExecutionResult?> LoadTestSequenceAsync(PreExecutionContext context)
    {
        var article = GetArticle(context);
        var result = await SequenceLoader.LoadRawDataAsync(article);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }
        context.RawSequenceData = result.RawData;
        Logger.LogInformation("Последовательность загружена: {Rows} строк", result.RawData!.Count);
        return null;
    }

    protected PreExecutionResult? BuildTestMaps(PreExecutionContext context)
    {
        var result = MapBuilder.Build(context.RawSequenceData!);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.Error!);
        }
        context.RawMaps = result.Maps;
        Logger.LogInformation("Построено карт тестов: {Count}", result.Maps!.Count);
        return null;
    }

    protected PreExecutionResult? ResolveTestMaps(PreExecutionContext context)
    {
        PhaseState.SetPhase(ExecutionPhase.ValidatingSteps);
        if (context.RawMaps is not { Count: > 0 })
        {
            return PreExecutionResult.Fail("Нет тестовых последовательностей", "Ошибка проверки шагов");
        }
        var resolveResult = MapResolver.Resolve(context.RawMaps);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            var error = $"Неизвестных шагов: {resolveResult.UnknownSteps.Count}";
            Logger.LogWarning("{Error}", error);
            return PreExecutionResult.Fail(error, new UnknownStepsDetails(resolveResult.UnknownSteps), "Ошибка проверки шагов");
        }
        context.Maps = resolveResult.Maps;
        Logger.LogInformation("Проверка шагов завершена: {Count} maps", context.Maps!.Count);
        return null;
    }

    private static string GetArticle(PreExecutionContext context)
    {
        return context.BoilerTypeCycle?.Article ?? context.BarcodeValidation!.Article!;
    }

    #endregion

    #region Валидация рецептов

    protected PreExecutionResult? ValidateRecipes(PreExecutionContext context)
    {
        PhaseState.SetPhase(ExecutionPhase.ValidatingRecipes);
        if (context.Maps == null || context.Maps.Count == 0)
        {
            return null;
        }
        var allSteps = ExtractAllSteps(context.Maps);
        var validation = RecipeValidator.Validate(allSteps, BoilerState.Recipes);
        if (!validation.IsValid)
        {
            var error = $"Отсутствуют рецепты: {validation.MissingRecipes.Count}";
            Logger.LogWarning("{Error}", error);
            return PreExecutionResult.Fail(
                error,
                new MissingRecipesDetails(validation.MissingRecipes),
                "Ошибка проверки рецептов");
        }
        Logger.LogInformation("Проверка рецептов пройдена");
        return null;
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

    #endregion

    #region Сохранение состояния

    protected void SaveBoilerState(PreExecutionContext context)
    {
        var validation = context.BarcodeValidation!;
        BoilerState.SetData(
            validation.Barcode,
            validation.Article!,
            isValid: true,
            context.BoilerTypeCycle,
            context.Recipes);
        Logger.LogInformation("Состояние котла сохранено: {Barcode}, {Article}",
            validation.Barcode, validation.Article);
    }

    #endregion

    #region Запись рецептов в PLC

    protected async Task<PreExecutionResult?> WriteRecipesToPlcAsync(PreExecutionContext context, CancellationToken ct)
    {
        var recipes = GetPlcRecipes();
        if (recipes.Count == 0)
        {
            return null;
        }
        return await WriteAllRecipesAsync(recipes, context, ct);
    }

    private List<RecipeResponseDto> GetPlcRecipes()
    {
        return BoilerState.Recipes?
            .Where(r => r.IsPlc)
            .ToList() ?? [];
    }

    private async Task<PreExecutionResult?> WriteAllRecipesAsync(
        List<RecipeResponseDto> recipes,
        PreExecutionContext context,
        CancellationToken ct)
    {
        var total = recipes.Count;
        for (var i = 0; i < total; i++)
        {
            PhaseState.SetPhase(ExecutionPhase.LoadingRecipes);
            var result = await WriteRecipeAsync(recipes[i], context, ct);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }

    private async Task<PreExecutionResult?> WriteRecipeAsync(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        Logger.LogInformation("WriteRecipe: Tag={Tag}, Address=[{Address}], Value={Value}, Type={Type}",
            recipe.TagName, recipe.Address, recipe.Value, recipe.PlcType);
        var writeResult = await WriteValueByTypeAsync(recipe, context, ct);
        if (!writeResult.Success)
        {
            var message = $"Ошибка записи {recipe.TagName}: {writeResult.Error}";
            Logger.LogError("{Error}", message);
            var errorInfo = new RecipeWriteErrorInfo(recipe.TagName, recipe.Address, recipe.Value, writeResult.Error!);
            return PreExecutionResult.Fail(message, new RecipeWriteErrorDetails([errorInfo]), "Ошибка загрузки рецептов");
        }
        Logger.LogInformation("Записан рецепт {Tag} = {Value}", recipe.TagName, recipe.Value);
        return null;
    }

    private async Task<WriteResult> WriteValueByTypeAsync(
        RecipeResponseDto recipe,
        PreExecutionContext context,
        CancellationToken ct)
    {
        return recipe.PlcType switch
        {
            PlcType.REAL => await WriteFloatAsync(recipe, context, ct),
            PlcType.INT16 => await WriteInt16Async(recipe, context, ct),
            PlcType.DINT => await WriteInt32Async(recipe, context, ct),
            PlcType.BOOL => await context.OpcUa.WriteAsync(recipe.Address, ParseBool(recipe.Value), ct),
            PlcType.STRING => await context.OpcUa.WriteAsync(recipe.Address, recipe.Value, ct),
            _ => new WriteResult(recipe.Address, $"Неизвестный тип: {recipe.PlcType}")
        };
    }

    private async Task<WriteResult> WriteFloatAsync(RecipeResponseDto recipe, PreExecutionContext context, CancellationToken ct)
    {
        if (!TryParseFloat(recipe.Value, out var value))
        {
            return new WriteResult(recipe.Address, $"Некорректное значение '{recipe.Value}' для типа float");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
    }

    private async Task<WriteResult> WriteInt16Async(RecipeResponseDto recipe, PreExecutionContext context, CancellationToken ct)
    {
        if (!short.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var value))
        {
            return new WriteResult(recipe.Address, $"Некорректное значение '{recipe.Value}' для типа Int16");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
    }

    private async Task<WriteResult> WriteInt32Async(RecipeResponseDto recipe, PreExecutionContext context, CancellationToken ct)
    {
        if (!int.TryParse(recipe.Value, CultureInfo.InvariantCulture, out var value))
        {
            return new WriteResult(recipe.Address, $"Некорректное значение '{recipe.Value}' для типа Dint");
        }
        return await context.OpcUa.WriteAsync(recipe.Address, value, ct);
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

    #endregion

    #region Инициализация провайдера рецептов

    protected void InitializeRecipeProvider()
    {
        var recipes = BoilerState.Recipes ?? [];
        RecipeProvider.SetRecipes(recipes);
        Logger.LogInformation("Рецепты загружены в провайдер: {Count}", recipes.Count);
    }

    #endregion
}
