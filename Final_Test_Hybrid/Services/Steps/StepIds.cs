namespace Final_Test_Hybrid.Services.Steps;

/// <summary>
/// Константы идентификаторов шагов для синхронизации между шагами и ошибками.
/// </summary>
public static class StepIds
{
    // Pre-execution steps
    public const string InitializeDatabase = "initialize-database";
    public const string InitializeRecipeProvider = "initialize-recipe-provider";
    public const string ValidateRecipes = "validate-recipes";
    public const string ResolveTestMaps = "resolve-test-maps";
    public const string WriteRecipesToPlc = "write-recipes-to-plc";

    // Scan steps
    public const string ScanBarcode = "scan-barcode";
    public const string ScanBarcodeMes = "scan-barcode-mes";

    // Test steps
    public const string CheckResistance = "check-resistance";
    public const string MeasureVoltage = "measure-voltage";
    public const string PrintLabel = "print-label";
}
