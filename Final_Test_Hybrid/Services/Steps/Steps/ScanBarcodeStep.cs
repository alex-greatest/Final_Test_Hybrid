using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    BoilerService boilerService,
    OperationService operationService,
    BoilerState boilerState,
    OperatorState operatorState,
    ShiftState shiftState,
    RecipeTagValidator tagValidator,
    RequiredTagValidator requiredTagValidator,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ILogger<ScanBarcodeStep> logger,
    ITestStepLogger testStepLogger) : ITestStep, IScanBarcodeStep
{
    public string Id => "scan-barcode";
    public string Name => "Сканирование штрихкода";
    public string Description => "Сканирует штрихкод с продукта";
    public bool IsVisibleInEditor => false;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public async Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode)
    {
        testStepLogger.LogStepStart(Name);
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        var ctx = new BarcodeContext(barcode);
        return ValidateBarcode(ctx)
            ?? await FindCycleAsync(ctx)
            ?? await LoadRecipesAsync(ctx)
            /*?? await CheckTagsAsync(ctx)
            ?? CheckRequiredTags(ctx)*/
            ?? await LoadTestSequenceAsync(ctx)
            ?? BuildTestMaps(ctx)
            ?? await SuccessAsync(ctx);
    }

    private BarcodeStepResult? ValidateBarcode(BarcodeContext ctx)
    {
        ctx.Validation = barcodeScanService.Validate(ctx.Barcode);
        return ctx.Validation.IsValid ? null : Fail(ctx.Validation.Error!);
    }

    private async Task<BarcodeStepResult?> FindCycleAsync(BarcodeContext ctx)
    {
        var cycle = await boilerTypeService.FindActiveByArticleAsync(ctx.Validation.Article!);
        if (cycle == null)
        {
            return Fail("Тип котла не найден", LogLevel.Warning);
        }
        ctx.Cycle = cycle;
        return null;
    }

    private async Task<BarcodeStepResult?> LoadRecipesAsync(BarcodeContext ctx)
    {
        var recipes = await recipeService.GetByBoilerTypeIdAsync(ctx.Cycle.BoilerTypeId);
        ctx.Recipes = MapToRecipeResponseDtos(recipes);
        if (ctx.Recipes.Count == 0)
        {
            return Fail("Рецепты не найдены", LogLevel.Warning);
        }
        LogRecipes(ctx.Recipes);
        return null;
    }

    private void LogRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        LogInfo("Загружено рецептов: {Count}", recipes.Count);
        foreach (var recipe in recipes)
        {
            LogInfo("Рецепт: {TagName} = {Value} ({PlcType})", recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    private async Task<BarcodeStepResult?> CheckTagsAsync(BarcodeContext ctx)
    {
        var result = await tagValidator.ValidateAsync(ctx.Recipes);
        if (result.Success)
        {
            return null;
        }
        boilerState.Clear();
        return BarcodeStepResult.FailPlcTags(result.ErrorMessage!, result.MissingTags);
    }

    private BarcodeStepResult? CheckRequiredTags(BarcodeContext ctx)
    {
        var result = requiredTagValidator.Validate(ctx.Recipes);
        if (result.Success)
        {
            return null;
        }
        boilerState.Clear();
        return BarcodeStepResult.FailRequiredTags(result.ErrorMessage!, result.MissingTags);
    }

    private async Task<BarcodeStepResult?> LoadTestSequenceAsync(BarcodeContext ctx)
    {
        var result = await sequenceLoader.LoadRawDataAsync(ctx.Validation.Article!);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!, LogLevel.Warning);
        }
        ctx.RawSequenceData = result.RawData;
        return null;
    }

    private BarcodeStepResult? BuildTestMaps(BarcodeContext ctx)
    {
        var result = mapBuilder.Build(ctx.RawSequenceData!);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!, LogLevel.Error);
        }
        ctx.RawMaps = result.Maps;
        return null;
    }

    private BarcodeStepResult Fail(string error, LogLevel level = LogLevel.None)
    {
        LogByLevel(error, level);
        boilerState.Clear();
        return BarcodeStepResult.Fail(error);
    }

    private void LogByLevel(string message, LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Warning:
                logger.LogWarning("{Error}", message);
                testStepLogger.LogWarning("{Error}", message);
                break;
            case LogLevel.Error:
                logger.LogError("{Error}", message);
                testStepLogger.LogError(null, "{Error}", message);
                break;
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
            case LogLevel.Critical:
            case LogLevel.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    private async Task<BarcodeStepResult> SuccessAsync(BarcodeContext ctx)
    {
        logger.LogInformation("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}, RawMaps: {Maps}",
            ctx.Validation.Barcode, ctx.Validation.Article, ctx.Cycle.Type, ctx.Recipes.Count, ctx.RawMaps?.Count ?? 0);
        testStepLogger.LogInformation("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}, RawMaps: {Maps}",
            ctx.Validation.Barcode, ctx.Validation.Article, ctx.Cycle.Type, ctx.Recipes.Count, ctx.RawMaps?.Count ?? 0);
        var operatorName = operatorState.Username ?? "Unknown";
        var boiler = await boilerService.FindOrCreateAsync(ctx.Validation.Barcode, ctx.Cycle.Id, operatorName);
        await operationService.CreateAsync(boiler.Id, operatorName, shiftState.ShiftNumber ?? 0);
        boilerState.SetData(ctx.Validation.Barcode, ctx.Validation.Article!, isValid: true, ctx.Cycle, ctx.Recipes);
        testStepLogger.LogStepEnd(Name);
        return BarcodeStepResult.Pass(ctx.RawMaps!);
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
