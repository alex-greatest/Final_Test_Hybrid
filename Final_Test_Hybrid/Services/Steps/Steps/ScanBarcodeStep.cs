using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    BoilerState boilerState,
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

    public Task<StepResult> ProcessBarcodeAsync(string barcode)
    {
        testStepLogger.LogStepStart(Name);
        return ValidateBarcode(barcode)
            .ThenAsync(FindBoilerTypeAsync)
            .ThenAsync(LoadRecipesAsync)
            .Map(SaveSuccessState);
    }

    private Result<BarcodeValidationResult> ValidateBarcode(string barcode)
    {
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        var validation = barcodeScanService.Validate(barcode);
        if (validation.IsValid)
        {
            return Result<BarcodeValidationResult>.Ok(validation);
        }
        boilerState.SetData(barcode, "", isValid: false);
        return Result<BarcodeValidationResult>.Fail(StepResult.Fail(validation.Error!));
    }

    private async Task<Result<(BarcodeValidationResult Validation, BoilerTypeCycle Cycle)>> FindBoilerTypeAsync(
        BarcodeValidationResult validation)
    {
        try
        {
            var cycle = await boilerTypeService.FindActiveByArticleAsync(validation.Article!);
            if (cycle != null)
            {
                return Result<(BarcodeValidationResult, BoilerTypeCycle)>.Ok((validation, cycle));
            }
            logger.LogWarning("Тип котла не найден: {Article}", validation.Article);
            boilerState.SetData(validation.Barcode, validation.Article!, isValid: false);
            return Result<(BarcodeValidationResult, BoilerTypeCycle)>.Fail(StepResult.Fail("Тип котла не найден"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка БД: {Article}", validation.Article);
            return Result<(BarcodeValidationResult, BoilerTypeCycle)>.Fail(StepResult.WithError("Ошибка БД"));
        }
    }

    private async Task<Result<(BarcodeValidationResult Validation, BoilerTypeCycle Cycle, IReadOnlyList<RecipeResponseDto> Recipes)>> LoadRecipesAsync(
        (BarcodeValidationResult Validation, BoilerTypeCycle Cycle) context)
    {
        var recipes = await recipeService.GetByBoilerTypeIdAsync(context.Cycle.BoilerTypeId);
        if (recipes.Count == 0)
        {
            logger.LogWarning("Рецепты не найдены: {Id}", context.Cycle.BoilerTypeId);
            boilerState.Clear();
            return Result<(BarcodeValidationResult, BoilerTypeCycle, IReadOnlyList<RecipeResponseDto>)>.Fail(
                StepResult.Fail("Рецепты не найдены"));
        }
        var recipeDtos = MapToRecipeResponseDtos(recipes);
        return Result<(BarcodeValidationResult, BoilerTypeCycle, IReadOnlyList<RecipeResponseDto>)>.Ok(
            (context.Validation, context.Cycle, recipeDtos));
    }

    private StepResult SaveSuccessState(
        (BarcodeValidationResult Validation, BoilerTypeCycle Cycle, IReadOnlyList<RecipeResponseDto> Recipes) context)
    {
        logger.LogInformation("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}",
            context.Validation.Barcode, context.Validation.Article, context.Cycle.Type, context.Recipes.Count);
        boilerState.SetData(
            context.Validation.Barcode,
            context.Validation.Article!,
            isValid: true,
            context.Cycle,
            context.Recipes);
        testStepLogger.LogStepEnd(Name);
        return StepResult.Pass();
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
