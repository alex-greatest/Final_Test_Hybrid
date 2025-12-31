using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionStepRegistry(
    IEnumerable<IPreExecutionStep> steps,
    AppSettingsService appSettings) : IPreExecutionStepRegistry
{
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";
    private readonly List<IPreExecutionStep> _steps = steps.ToList();

    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
    {
        var targetId = appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
        return _steps.Where(s => s.Id == targetId).ToList();
    }
}
