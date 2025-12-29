using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class BarcodeProcessingPipeline(
    AppSettingsService appSettings,
    ITestStepRegistry stepRegistry,
    ITestMapResolver mapResolver,
    TestSequenseService sequenseService,
    RecipeValidator recipeValidator,
    BoilerState boilerState,
    IRecipeProvider recipeProvider,
    TestExecutionCoordinator coordinator,
    ILogger<BarcodeProcessingPipeline> logger)
{
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";

    public async Task<BarcodeProcessingResult> ProcessAsync(string barcode)
    {
        try
        {
            return await ProcessBarcodeCore(barcode);
        }
        catch (Exception ex)
        {
            return HandleScannerError(ex, barcode);
        }
    }

    private async Task<BarcodeProcessingResult> ProcessBarcodeCore(string barcode)
    {
        sequenseService.ClearAll();
        var testStep = GetCurrentTestStep();
        var scanStep = (IScanBarcodeStep)testStep;
        var scanStepId = sequenseService.AddStep(testStep);
        var result = await scanStep.ProcessBarcodeAsync(barcode);
        if (!result.IsSuccess)
        {
            return HandleBarcodeFailure(result, scanStepId);
        }
        return ResolveAndValidateMaps(result.RawMaps!, scanStepId);
    }

    private ITestStep GetCurrentTestStep()
    {
        var stepId = GetCurrentStepId();
        return stepRegistry.GetById(stepId)!;
    }

    private string GetCurrentStepId()
    {
        return appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
    }

    private BarcodeProcessingResult HandleScannerError(Exception ex, string barcode)
    {
        logger.LogError(ex, "Ошибка сканера: {Barcode}", barcode);
        return BarcodeProcessingResult.Fail(Guid.Empty, "Ошибка сканера");
    }

    private static BarcodeProcessingResult HandleBarcodeFailure(BarcodeStepResult result, Guid scanStepId)
    {
        if (result.MissingPlcTags.Count > 0)
        {
            return BarcodeProcessingResult.WithMissingPlcTags(scanStepId, result.ErrorMessage!, result.MissingPlcTags);
        }
        if (result.MissingRequiredTags.Count > 0)
        {
            return BarcodeProcessingResult.WithMissingRequiredTags(scanStepId, result.ErrorMessage!, result.MissingRequiredTags);
        }
        return BarcodeProcessingResult.Fail(scanStepId, result.ErrorMessage!);
    }

    private BarcodeProcessingResult ResolveAndValidateMaps(List<RawTestMap> rawMaps, Guid scanStepId)
    {
        var resolveResult = mapResolver.Resolve(rawMaps);
        if (resolveResult.UnknownSteps.Count > 0)
        {
            return HandleUnknownSteps(resolveResult.UnknownSteps, scanStepId);
        }
        return ValidateRecipesAndStart(resolveResult.Maps!, scanStepId);
    }

    private static BarcodeProcessingResult HandleUnknownSteps(IReadOnlyList<UnknownStepInfo> unknownSteps, Guid scanStepId)
    {
        var error = $"Неизвестных шагов: {unknownSteps.Count}";
        return BarcodeProcessingResult.WithUnknownSteps(scanStepId, error, unknownSteps);
    }

    private BarcodeProcessingResult ValidateRecipesAndStart(List<TestMap> maps, Guid scanStepId)
    {
        var allSteps = ExtractAllSteps(maps);
        var recipeValidation = recipeValidator.Validate(allSteps, boilerState.Recipes);
        if (!recipeValidation.IsValid)
        {
            return HandleMissingRecipes(recipeValidation.MissingRecipes, scanStepId);
        }
        SetRecipesAndStartExecution(maps, scanStepId);
        return BarcodeProcessingResult.Success(scanStepId);
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

    private static BarcodeProcessingResult HandleMissingRecipes(
        IReadOnlyList<MissingRecipeInfo> missingRecipes,
        Guid scanStepId)
    {
        var error = $"Отсутствуют рецепты: {missingRecipes.Count}";
        return BarcodeProcessingResult.WithMissingRecipes(scanStepId, error, missingRecipes);
    }

    private void SetRecipesAndStartExecution(List<TestMap> maps, Guid scanStepId)
    {
        recipeProvider.SetRecipes(boilerState.Recipes ?? []);
        StartExecution(maps, scanStepId);
    }

    private void StartExecution(List<TestMap> maps, Guid scanStepId)
    {
        logger.LogInformation("Запуск выполнения {Count} maps", maps.Count);
        sequenseService.SetSuccess(scanStepId);
        coordinator.SetMaps(maps);
        _ = coordinator.StartAsync();
    }
}
