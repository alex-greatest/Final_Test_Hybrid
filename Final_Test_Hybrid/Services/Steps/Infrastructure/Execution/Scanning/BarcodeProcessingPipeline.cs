using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public class BarcodeProcessingPipeline(
    AppSettingsService appSettings,
    ITestStepRegistry stepRegistry,
    ITestMapResolver mapResolver,
    StepStatusReporter statusReporter,
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
        statusReporter.ClearAll();
        var testStep = GetCurrentTestStep();
        var scanStep = (IScanBarcodeStep)testStep;
        var scanStepId = statusReporter.ReportStepStarted(testStep);
        var result = await scanStep.ProcessBarcodeAsync(barcode);
        return !result.IsSuccess ? HandleBarcodeFailure(result, scanStepId) : ResolveAndValidateMaps(result.RawMaps!, scanStepId);
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

    private BarcodeProcessingResult HandleBarcodeFailure(BarcodeStepResult result, Guid scanStepId)
    {
        statusReporter.ReportError(scanStepId, result.ErrorMessage!);
        if (result.MissingPlcTags.Count > 0)
        {
            return BarcodeProcessingResult.WithMissingPlcTags(scanStepId, result.ErrorMessage!, result.MissingPlcTags);
        }
        return result.MissingRequiredTags.Count > 0 ? BarcodeProcessingResult.WithMissingRequiredTags(scanStepId, result.ErrorMessage!, result.MissingRequiredTags) : BarcodeProcessingResult.Fail(scanStepId, result.ErrorMessage!);
    }

    private BarcodeProcessingResult ResolveAndValidateMaps(List<RawTestMap> rawMaps, Guid scanStepId)
    {
        var resolveResult = mapResolver.Resolve(rawMaps);
        return resolveResult.UnknownSteps.Count > 0 ? HandleUnknownSteps(resolveResult.UnknownSteps, scanStepId) : ValidateRecipesAndStart(resolveResult.Maps!, scanStepId);
    }

    private BarcodeProcessingResult HandleUnknownSteps(IReadOnlyList<UnknownStepInfo> unknownSteps, Guid scanStepId)
    {
        var error = $"Неизвестных шагов: {unknownSteps.Count}";
        statusReporter.ReportError(scanStepId, error);
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

    private BarcodeProcessingResult HandleMissingRecipes(
        IReadOnlyList<MissingRecipeInfo> missingRecipes,
        Guid scanStepId)
    {
        var error = $"Отсутствуют рецепты: {missingRecipes.Count}";
        statusReporter.ReportError(scanStepId, error);
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
        statusReporter.ReportSuccess(scanStepId);
        coordinator.SetMaps(maps);
        _ = coordinator.StartAsync();
    }
}
