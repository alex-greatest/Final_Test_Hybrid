using Final_Test_Hybrid.Models.Database;
using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeStep(
    BarcodeScanService barcodeScanService,
    BoilerTypeService boilerTypeService,
    BoilerState boilerState,
    ILogger<ScanBarcodeStep> logger) : ITestStep, IScanBarcodeStep
{
    public string Id => "scan-barcode";
    public string Name => "Сканирование штрихкода";
    public string Description => "Сканирует штрихкод с продукта";
    public bool IsVisibleInEditor => false;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public async Task<StepResult> ProcessBarcodeAsync(string barcode)
    {
        logger.LogInformation("Обработка штрихкода: {Barcode}", barcode);
        var validation = barcodeScanService.Validate(barcode);
        if (!validation.IsValid)
        {
            return HandleInvalidBarcode(barcode, validation.Error!);
        }
        return await FindBoilerTypeAsync(validation);
    }

    private StepResult HandleInvalidBarcode(string barcode, string error)
    {
        boilerState.SetData(barcode, "", isValid: false);
        return StepResult.Fail(error);
    }

    private async Task<StepResult> FindBoilerTypeAsync(BarcodeValidationResult validation)
    {
        try
        {
            return await TryFindBoilerTypeAsync(validation);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка БД: {Article}", validation.Article);
            return StepResult.WithError("Ошибка БД");
        }
    }

    private async Task<StepResult> TryFindBoilerTypeAsync(BarcodeValidationResult validation)
    {
        var cycle = await boilerTypeService.FindActiveByArticleAsync(validation.Article!);
        return cycle == null ? HandleBoilerTypeNotFound(validation) : HandleSuccess(validation, cycle);
    }

    private StepResult HandleBoilerTypeNotFound(BarcodeValidationResult validation)
    {
        logger.LogWarning("Тип котла не найден: {Article}", validation.Article);
        boilerState.SetData(validation.Barcode, validation.Article!, isValid: false);
        return StepResult.Fail("Тип котла не найден");
    }

    private StepResult HandleSuccess(BarcodeValidationResult validation, BoilerTypeCycle cycle)
    {
        logger.LogInformation("Успешно: {Serial}, {Article}, {Type}",
            validation.Barcode, validation.Article, cycle.Type);
        boilerState.SetData(validation.Barcode, validation.Article!, isValid: true, cycle);
        return StepResult.Pass();
    }
}
