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
    private const string ValidateRecipesId = "validate-recipes";
    private const string InitializeDatabaseId = "initialize-database";
    private const string InitializeRecipeProviderId = "initialize-recipe-provider";
    private readonly List<IPreExecutionStep> _steps = steps.ToList();

    public IReadOnlyList<IPreExecutionStep> GetOrderedSteps()
    {
        var scanId = appSettings.UseMes ? ScanBarcodeMesId : ScanBarcodeId;
        var scanStep = GetStep(scanId);
        var resolveStep = GetStep(ResolveTestMapsId);
        var validateRecipesStep = GetStep(ValidateRecipesId);
        var initDbStep = GetStep(InitializeDatabaseId);
        var writeRecipesStep = GetStep(WriteRecipesId);
        var initRecipeProviderStep = GetStep(InitializeRecipeProviderId);
        if (!AreAllStepsPresent(scanStep, resolveStep, validateRecipesStep, initDbStep, writeRecipesStep, initRecipeProviderStep))
        {
            return [];
        }
        // Порядок: Scan → Resolve → ValidateRecipes → InitDb → WriteRecipes → InitRecipeProvider
        var scanGroup = new PreExecutionStepGroup(
            scanStep!,
            resolveStep!,
            validateRecipesStep!,
            initDbStep!,
            writeRecipesStep!,
            initRecipeProviderStep!);
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
