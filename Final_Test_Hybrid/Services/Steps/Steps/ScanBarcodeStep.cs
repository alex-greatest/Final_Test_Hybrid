using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    BoilerState boilerState,
    RecipeTagValidator tagValidator,
    RequiredTagValidator requiredTagValidator,
    ITestSequenceLoader sequenceLoader,
    ITestMapBuilder mapBuilder,
    ExecutionMessageState messageState,
    ILogger<ScanBarcodeStep> logger,
    ITestStepLogger testStepLogger) : ITestStep, IScanBarcodeStep, IPreExecutionStep
{
    public string Id => "scan-barcode";
    public string Name => "Сканирование штрихкода";
    public string Description => "Сканирует штрихкод с продукта";
    public bool IsVisibleInEditor => false;
    public bool IsVisibleInStatusGrid => true;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public async Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode)
    {
        testStepLogger.LogStepStart(Name);
        LogInfo("Обработка штрихкода: {Barcode}", barcode);
        var pipeline = new BarcodePipeline(barcode);
        var result = await ExecutePipelineAsync(pipeline);
        return !result.IsSuccess ? result : CompleteSuccessfully(pipeline);
    }

    private async Task<BarcodeStepResult> ExecutePipelineAsync(BarcodePipeline pipeline)
    {
        return await pipeline
            .Step("Проверка штрихкода...", ValidateBarcode)
            .StepAsync("Поиск типа котла...", FindBoilerTypeAsync)
            .StepAsync("Загрузка рецептов...", LoadRecipesAsync)
            .StepAsync("Загрузка последовательности...", LoadTestSequenceAsync)
            .Step("Построение карт тестов...", BuildTestMaps)
            .ExecuteAsync(messageState);
    }

    private BarcodeStepResult? ValidateBarcode(BarcodePipeline pipeline)
    {
        pipeline.Validation = barcodeScanService.Validate(pipeline.Barcode);
        return !pipeline.Validation.IsValid ? Fail(pipeline.Validation.Error!) : null;
    }

    private async Task<BarcodeStepResult?> FindBoilerTypeAsync(BarcodePipeline pipeline)
    {
        var cycle = await boilerTypeService.FindActiveByArticleAsync(pipeline.Validation.Article!);
        if (cycle == null)
        {
            return Fail("Тип котла не найден");
        }
        pipeline.Cycle = cycle;
        return null;
    }

    private async Task<BarcodeStepResult?> LoadRecipesAsync(BarcodePipeline pipeline)
    {
        var recipes = await recipeService.GetByBoilerTypeIdAsync(pipeline.Cycle.BoilerTypeId);
        pipeline.Recipes = MapToRecipeResponseDtos(recipes);

        if (pipeline.Recipes.Count == 0)
        {
            return Fail("Рецепты не найдены");
        }
        LogLoadedRecipes(pipeline.Recipes);
        return null;
    }

    private void LogLoadedRecipes(IReadOnlyList<RecipeResponseDto> recipes)
    {
        LogInfo("Загружено рецептов: {Count}", recipes.Count);
        foreach (var recipe in recipes)
        {
            LogInfo("Рецепт: {TagName} = {Value} ({PlcType})", recipe.TagName, recipe.Value, recipe.PlcType);
        }
    }

    private async Task<BarcodeStepResult?> LoadTestSequenceAsync(BarcodePipeline pipeline)
    {
        var result = await sequenceLoader.LoadRawDataAsync(pipeline.Validation.Article!);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!);
        }
        pipeline.RawSequenceData = result.RawData!;
        return null;
    }

    private BarcodeStepResult? BuildTestMaps(BarcodePipeline pipeline)
    {
        var result = mapBuilder.Build(pipeline.RawSequenceData);
        if (!result.IsSuccess)
        {
            return Fail(result.Error!);
        }
        pipeline.RawMaps = result.Maps!;
        return null;
    }

    private BarcodeStepResult CompleteSuccessfully(BarcodePipeline pipeline)
    {
        LogSuccessfulProcessing(pipeline);
        SaveBoilerState(pipeline);
        testStepLogger.LogStepEnd(Name);
        return BarcodeStepResult.Pass(pipeline.RawMaps);
    }

    private void LogSuccessfulProcessing(BarcodePipeline pipeline)
    {
        LogInfo("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}, RawMaps: {Maps}",
            pipeline.Validation.Barcode,
            pipeline.Validation.Article,
            pipeline.Cycle.Type,
            pipeline.Recipes.Count,
            pipeline.RawMaps.Count);
    }

    private void SaveBoilerState(BarcodePipeline pipeline)
    {
        boilerState.SetData(
            pipeline.Validation.Barcode,
            pipeline.Validation.Article!,
            isValid: true,
            pipeline.Cycle,
            pipeline.Recipes);
    }

    private BarcodeStepResult Fail(string error)
    {
        testStepLogger.LogError(null, "{Error}", error);
        return BarcodeStepResult.Fail(error);
    }

    private void LogInfo(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    async Task<PreExecutionResult> IPreExecutionStep.ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await ProcessBarcodeAsync(context.Barcode);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.ErrorMessage!);
        }
        context.RawMaps = result.RawMaps;
        return PreExecutionResult.Continue();
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

    // Currently disabled tag validation methods - preserved for future use
    private async Task<BarcodeStepResult?> CheckTagsAsync(BarcodePipeline pipeline)
    {
        var result = await tagValidator.ValidateAsync(pipeline.Recipes);
        return result.Success ? null : BarcodeStepResult.FailPlcTags(result.ErrorMessage!, result.MissingTags);
    }

    private BarcodeStepResult? CheckRequiredTags(BarcodePipeline pipeline)
    {
        var result = requiredTagValidator.Validate(pipeline.Recipes);
        return result.Success ? null : BarcodeStepResult.FailRequiredTags(result.ErrorMessage!, result.MissingTags);
    }
}
