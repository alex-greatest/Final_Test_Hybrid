using Final_Test_Hybrid.Models.Database;
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
    public IReadOnlyList<string> LastMissingTags { get; private set; } = [];

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public async Task<StepResult> ProcessBarcodeAsync(string barcode)
    {
        testStepLogger.LogStepStart(Name);
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        LastMissingTags = [];

        var (validation, e1) = ValidateBarcode(barcode);
        var (cycle, e2) = await FindCycleAsync(validation);
        var (recipes, e3) = await LoadRecipesAsync(cycle);
        var e4 = await CheckTagsAsync(recipes);

        return e1 ?? e2 ?? e3 ?? e4 ?? SaveSuccess(validation!, cycle!, recipes!);
    }

    private (BarcodeValidationResult?, StepResult?) ValidateBarcode(string barcode)
    {
        var v = barcodeScanService.Validate(barcode);
        if (v.IsValid) return (v, null);
        boilerState.SetData(barcode, "", isValid: false);
        return (null, StepResult.Fail(v.Error!));
    }

    private async Task<(BoilerTypeCycle?, StepResult?)> FindCycleAsync(BarcodeValidationResult? v)
    {
        if (v == null) return (null, null);
        var cycle = await boilerTypeService.FindActiveByArticleAsync(v.Article!);
        if (cycle != null) return (cycle, null);
        logger.LogWarning("Тип котла не найден: {Article}", v.Article);
        boilerState.SetData(v.Barcode, v.Article!, isValid: false);
        return (null, StepResult.Fail("Тип котла не найден"));
    }

    private async Task<(IReadOnlyList<RecipeResponseDto>?, StepResult?)> LoadRecipesAsync(BoilerTypeCycle? cycle)
    {
        if (cycle == null) return (null, null);
        var recipes = await recipeService.GetByBoilerTypeIdAsync(cycle.BoilerTypeId);
        var dtos = MapToRecipeResponseDtos(recipes);
        return dtos.Count != 0 ? (dtos, null) : OnEmptyRecipes(cycle.BoilerTypeId);
    }

    private (IReadOnlyList<RecipeResponseDto>?, StepResult?) OnEmptyRecipes(long boilerTypeId)
    {
        logger.LogWarning("Рецепты не найдены: {Id}", boilerTypeId);
        boilerState.Clear();
        return (null, StepResult.Fail("Рецепты не найдены"));
    }

    private async Task<StepResult?> CheckTagsAsync(IReadOnlyList<RecipeResponseDto>? recipes)
    {
        if (recipes == null) return null;
        var result = await tagValidator.ValidateAsync(recipes);
        return result.Success ? null : OnTagValidationFailed(result);
    }

    private StepResult OnTagValidationFailed(TagValidationResult result)
    {
        LastMissingTags = result.MissingTags;
        boilerState.Clear();
        return StepResult.Fail(result.ErrorMessage!);
    }

    private StepResult SaveSuccess(BarcodeValidationResult v, BoilerTypeCycle cycle, IReadOnlyList<RecipeResponseDto> recipes)
    {
        logger.LogInformation("Успешно: {Serial}, {Article}, {Type}, рецептов: {Count}",
            v.Barcode, v.Article, cycle.Type, recipes.Count);
        boilerState.SetData(v.Barcode, v.Article!, isValid: true, cycle, recipes);
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
