using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Steps.PreExecution;

public class SaveBoilerStateStep(
    BoilerState boilerState,
    DualLogger<SaveBoilerStateStep> logger) : IPreExecutionStep
{
    public string Id => "save-boiler-state";
    public string Name => "Сохранение состояния";
    public string Description => "Сохранение данных котла";
    public bool IsVisibleInStatusGrid => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        var validation = context.BarcodeValidation!;
        boilerState.SetData(
            validation.Barcode,
            validation.Article!,
            isValid: true,
            context.BoilerTypeCycle,
            context.Recipes);
        logger.LogInformation("Состояние котла сохранено: {Barcode}, {Article}",
            validation.Barcode, validation.Article);
        return Task.FromResult(PreExecutionResult.Continue());
    }
}
