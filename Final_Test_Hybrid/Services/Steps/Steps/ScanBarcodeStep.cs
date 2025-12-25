using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    BoilerTypeService boilerTypeService,
    RecipeService recipeService,
    BoilerState boilerState,
    RecipeTagValidator tagValidator,
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
            ?? await CheckTagsAsync(ctx)
            ?? Success(ctx);
    }

    private BarcodeStepResult? ValidateBarcode(BarcodeContext ctx)
    {
        ctx.Validation = barcodeScanService.Validate(ctx.Barcode);
        if (ctx.Validation.IsValid)
        {
            return null;
        }
        boilerState.SetData(ctx.Barcode, "", isValid: false);
        return BarcodeStepResult.Fail(ctx.Validation.Error!);
    }

    private async Task<BarcodeStepResult?> FindCycleAsync(BarcodeContext ctx)
    {
        var cycle = await boilerTypeService.FindActiveByArticleAsync(ctx.Validation.Article!);
        if (cycle == null)
        {
            logger.LogWarning("Тип котла не найден: {Article}", ctx.Validation.Article);
            boilerState.SetData(ctx.Validation.Barcode, ctx.Validation.Article!, isValid: false);
            return BarcodeStepResult.Fail("Тип котла не найден");
        }
        ctx.Cycle = cycle;
        return null;
    }

    private async Task<BarcodeStepResult?> LoadRecipesAsync(BarcodeContext ctx)
    {
        var recipes = await recipeService.GetByBoilerTypeIdAsync(ctx.Cycle.BoilerTypeId);
        ctx.Recipes = MapToRecipeResponseDtos(recipes);
        if (ctx.Recipes.Count != 0)
        {
            return null;
        }
        logger.LogWarning("Рецепты не найдены: {Id}", ctx.Cycle.BoilerTypeId);
        boilerState.Clear();
        return BarcodeStepResult.Fail("Рецепты не найдены");
    }

    private async Task<BarcodeStepResult?> CheckTagsAsync(BarcodeContext ctx)
    {
        var result = await tagValidator.ValidateAsync(ctx.Recipes);
        if (result.Success)
        {
            return null;
        }
        boilerState.Clear();
        return BarcodeStepResult.Fail(result.ErrorMessage!, result.MissingTags);
    }

    private BarcodeStepResult Success(BarcodeContext ctx)
    {
        logger.LogInformation("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}",
            ctx.Validation.Barcode, ctx.Validation.Article, ctx.Cycle.Type, ctx.Recipes.Count);
        boilerState.SetData(ctx.Validation.Barcode, ctx.Validation.Article!, isValid: true, ctx.Cycle, ctx.Recipes);
        testStepLogger.LogStepEnd(Name);
        return BarcodeStepResult.Pass();
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
