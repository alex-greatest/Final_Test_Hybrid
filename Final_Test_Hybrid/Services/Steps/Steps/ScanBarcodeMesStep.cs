using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure;
using Final_Test_Hybrid.Services.Steps.Interaces;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeMesStep(
    BarcodeScanService barcodeScanService,
    BoilerState boilerState,
    ILogger<ScanBarcodeMesStep> logger) : ITestStep, IScanBarcodeStep
{
    public string Id => "scan-barcode-mes";
    public string Name => "Сканирование штрихкода MES";
    public string Description => "Сканирует штрихкод и отправляет в MES";
    public bool IsVisibleInEditor => false;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public Task<StepResult> ProcessBarcodeAsync(string barcode)
    {
        logger.LogInformation("Обработка штрихкода MES: {Barcode}", barcode);
        var validation = barcodeScanService.Validate(barcode);
        return Task.FromResult(!validation.IsValid ? HandleInvalidBarcode(barcode, validation.Error!) : HandleSuccess(validation));
    }

    private StepResult HandleInvalidBarcode(string barcode, string error)
    {
        boilerState.SetData(barcode, "", isValid: false);
        return StepResult.Fail(error);
    }

    private StepResult HandleSuccess(BarcodeValidationResult validation)
    {
        // TODO: Добавить MES-логику
        logger.LogInformation("MES: Серийный номер: {Serial}, Артикул: {Article}",
            validation.Barcode, validation.Article);
        boilerState.SetData(validation.Barcode, validation.Article!, isValid: true);
        return StepResult.Pass();
    }
}
