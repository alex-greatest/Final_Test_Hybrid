using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Steps;

public class ScanBarcodeMesStep(
    BarcodeScanService barcodeScanService,
    BoilerState boilerState,
    ILogger<ScanBarcodeMesStep> logger) : ITestStep, IScanBarcodeStep, IPreExecutionStep
{
    public string Id => "scan-barcode-mes";
    public string Name => "Сканирование штрихкода MES";
    public string Description => "Сканирует штрихкод и отправляет в MES";
    public bool IsVisibleInEditor => false;

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        return Task.FromResult(TestStepResult.Pass());
    }

    public Task<BarcodeStepResult> ProcessBarcodeAsync(string barcode)
    {
        logger.LogInformation("Обработка штрихкода MES: {Barcode}", barcode);
        var validation = barcodeScanService.Validate(barcode);
        return Task.FromResult(!validation.IsValid ? HandleInvalidBarcode(barcode, validation.Error!) : HandleSuccess(validation));
    }

    private BarcodeStepResult HandleInvalidBarcode(string barcode, string error)
    {
        boilerState.SetData(barcode, "", isValid: false);
        return BarcodeStepResult.Fail(error);
    }

    private BarcodeStepResult HandleSuccess(BarcodeValidationResult validation)
    {
        // TODO: Добавить MES-логику
        logger.LogInformation("MES: Серийный номер: {Serial}, Артикул: {Article}",
            validation.Barcode, validation.Article);
        boilerState.SetData(validation.Barcode, validation.Article!, isValid: true);
        return BarcodeStepResult.Pass([]);
    }

    public Task OnExecutionStartingAsync()
    {
        // TODO: Отправка в MES при необходимости
        return Task.CompletedTask;
    }

    async Task<PreExecutionResult> IPreExecutionStep.ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var result = await ProcessBarcodeAsync(context.Barcode);
        if (!result.IsSuccess)
        {
            return PreExecutionResult.Fail(result.ErrorMessage!);
        }
        context.RawMaps = result.RawMaps;
        return PreExecutionResult.Ok();
    }
}
