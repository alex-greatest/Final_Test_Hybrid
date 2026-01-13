using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

/// <summary>
/// Шаг сканирования для Non-MES режима.
/// Выполняет полную подготовку к тесту: валидация, поиск типа, загрузка рецептов, построение maps, инициализация БД.
/// </summary>
public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ITestMapResolver mapResolver,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    PausableOpcUaTagService opcUa,
    IRecipeProvider recipeProvider,
    ExecutionMessageState messageState,
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    BoilerService boilerService,
    OperationService operationService,
    OperatorState operatorState,
    ShiftState shiftState,
    ILogger<ScanBarcodeStep> logger,
    ITestStepLogger testStepLogger)
    : ScanStepBase(barcodeScanService, sequenceLoader, mapBuilder, mapResolver,
        recipeValidator, boilerState, opcUa, recipeProvider, messageState)
{
    private readonly DualLogger<ScanBarcodeStep> _logger = new(logger, testStepLogger);

    private const string UnknownOperator = "Unknown";

    public override string Id => "scan-barcode";
    public override string Name => "Сканирование штрихкода";
    public override string Description => "Сканирует штрихкод и выполняет подготовку к тесту";
    protected override IDualLogger Logger => _logger;

    public override async Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        _logger.LogStepStart(Name);
        MessageState.SetMessage("Штрихкод получен");

        // 1. Валидация баркода
        var validateError = ValidateBarcode(context);
        if (validateError != null) return validateError;

        // 2. Поиск типа котла в БД
        var findTypeError = await FindBoilerTypeAsync(context);
        if (findTypeError != null) return findTypeError;

        // 3. Загрузка рецептов из БД
        var loadRecipesError = await LoadRecipesAsync(context);
        if (loadRecipesError != null) return loadRecipesError;

        // 4. Загрузка последовательности тестов
        var loadSequenceError = await LoadTestSequenceAsync(context);
        if (loadSequenceError != null) return loadSequenceError;

        // 5. Построение карт тестов
        var buildMapsError = BuildTestMaps(context);
        if (buildMapsError != null) return buildMapsError;

        // 6. Сохранение состояния котла
        SaveBoilerState(context);

        // 7. Резолв карт тестов
        var resolveMapsError = ResolveTestMaps(context);
        if (resolveMapsError != null) return resolveMapsError;

        // 8. Валидация рецептов
        var validateRecipesError = ValidateRecipes(context);
        if (validateRecipesError != null) return validateRecipesError;

        // 9. Инициализация БД (создание записей)
        var initDbError = await InitializeDatabaseAsync(context);
        if (initDbError != null) return initDbError;

        // 10. Запись рецептов в PLC
        var writePlcError = await WriteRecipesToPlcAsync(context, ct);
        if (writePlcError != null) return writePlcError;

        // 11. Инициализация провайдера рецептов
        InitializeRecipeProvider();

        _logger.LogStepEnd(Name);
        return PreExecutionResult.Continue(context.Barcode);
    }

    private async Task<PreExecutionResult?> FindBoilerTypeAsync(PreExecutionContext context)
    {
        var article = context.BarcodeValidation!.Article!;
        var cycle = await boilerTypeService.FindActiveByArticleAsync(article);
        if (cycle == null)
        {
            return PreExecutionResult.Fail($"Тип котла не найден для артикула: {article}");
        }
        context.BoilerTypeCycle = cycle;
        _logger.LogInformation("Тип котла найден: {Type}, артикул: {Article}", cycle.Type, cycle.Article);
        return null;
    }

    private async Task<PreExecutionResult?> LoadRecipesAsync(PreExecutionContext context)
    {
        var boilerTypeId = context.BoilerTypeCycle!.BoilerTypeId;
        var recipes = await recipeService.GetByBoilerTypeIdAsync(boilerTypeId);
        if (recipes.Count == 0)
        {
            return PreExecutionResult.Fail("Рецепты не найдены");
        }
        context.Recipes = MapToRecipeResponseDtos(recipes);
        LogRecipes(context.Recipes);
        return null;
    }

    private async Task<PreExecutionResult?> InitializeDatabaseAsync(PreExecutionContext context)
    {
        if (string.IsNullOrEmpty(BoilerState.SerialNumber) || BoilerState.BoilerTypeCycle == null)
        {
            return PreExecutionResult.Fail("Данные котла не инициализированы", "Ошибка инициализации");
        }
        MessageState.SetMessage("Создание записей в БД...");
        var operatorName = operatorState.Username ?? UnknownOperator;
        var boiler = await boilerService.FindOrCreateAsync(
            BoilerState.SerialNumber,
            BoilerState.BoilerTypeCycle.Id,
            operatorName);
        await operationService.CreateAsync(boiler.Id, operatorName, shiftState.ShiftNumber ?? 0);
        _logger.LogInformation("Записи в БД созданы: Boiler={BoilerId}", boiler.Id);
        return null;
    }

    private void LogRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        _logger.LogInformation("Загружено рецептов: {Count}", recipes.Count);
        foreach (var recipe in recipes)
        {
            _logger.LogInformation("Рецепт: {TagName} = {Value} ({PlcType})",
                recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    private static IReadOnlyList<RecipeResponseDto> MapToRecipeResponseDtos(List<Recipe> recipes)
    {
        return recipes.Select(r => new RecipeResponseDto
        {
            TagName = r.TagName,
            Value = r.Value,
            Address = r.Address,
            PlcType = r.PlcType,
            IsPlc = r.IsPlc,
            Unit = r.Unit,
            Description = r.Description
        }).ToList();
    }
}
