using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class ValidateBarcodeStep(
    BarcodeScanService barcodeScanService,
    DualLogger<ValidateBarcodeStep> logger) : IPreExecutionStep
{
    public string Id => "validate-barcode";
    public string Name => "Проверка штрихкода";
    public string Description => "Валидация формата штрихкода";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var validation = barcodeScanService.Validate(context.Barcode);
        if (!validation.IsValid)
        {
            return Task.FromResult(PreExecutionResult.Fail(validation.Error!));
        }
        context.BarcodeValidation = validation;
        logger.LogInformation("Штрихкод валиден: {Barcode}, артикул: {Article}",
            validation.Barcode, validation.Article);
        return Task.FromResult(PreExecutionResult.Continue(context.Barcode));
    }
}
