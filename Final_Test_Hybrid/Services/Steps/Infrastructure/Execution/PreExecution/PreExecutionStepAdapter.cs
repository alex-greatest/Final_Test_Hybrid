using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionStepAdapter(
    AppSettingsService appSettings,
    ScanBarcodeStep scanBarcodeStep,
    ScanBarcodeMesStep scanBarcodeMesStep,
    BlockBoilerAdapterStep blockBoilerAdapterStep) : IPreExecutionStepRegistry
{
    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
    {
        IPreExecutionStep scanStep = appSettings.UseMes ? scanBarcodeMesStep : scanBarcodeStep;
        return [scanStep, blockBoilerAdapterStep];
    }
}
