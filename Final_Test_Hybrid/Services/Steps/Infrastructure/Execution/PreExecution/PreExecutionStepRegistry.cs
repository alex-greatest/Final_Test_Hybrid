using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.PreExecution;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public class PreExecutionStepRegistry(
    IEnumerable<IPreExecutionStep> steps,
    AppSettingsService appSettings) : IPreExecutionStepRegistry
{
    private const string ScanBarcodeId = "scan-barcode";
    private const string ScanBarcodeMesId = "scan-barcode-mes";
    private const string WriteRecipesId = "write-recipes-to-plc";
    private const string ResolveTestMapsId = "resolve-test-maps";
    private readonly List<IPreExecutionStep> _steps = steps.ToList();

    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
    {
        var scanId = appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
        var scanStep = GetStep(scanId);
        var writeRecipesStep = GetStep(WriteRecipesId);
        var resolveStep = GetStep(ResolveTestMapsId);
        if (!AreAllStepsPresent(scanStep, writeRecipesStep, resolveStep))
        {
            return [];
        }
        var scanGroup = new PreExecutionStepGroup(scanStep!, writeRecipesStep!, resolveStep!);
        return [scanGroup];
    }

    private static bool AreAllStepsPresent(params IPreExecutionStep?[] steps)
    {
        return steps.All(s => s != null);
    }

    private IPreExecutionStep? GetStep(string id)
    {
        return _steps.FirstOrDefault(s => s.Id == id);
    }
}
