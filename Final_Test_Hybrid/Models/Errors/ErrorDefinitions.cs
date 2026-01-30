namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Агрегация всех ошибок
    public static IReadOnlyList<ErrorDefinition> All =>
        [..GlobalPlcErrors, ..GlobalAppErrors, ..StepErrors, ..Steps1Errors, ..DiagnosticEcuErrors];

    // Хелперы
    public static IEnumerable<ErrorDefinition> PlcErrors => All.Where(e => e.IsPlcBound);
    public static ErrorDefinition? ByCode(string code) => All.FirstOrDefault(e => e.Code == code);
    public static ErrorDefinition? ByPlcTag(string tag) => All.FirstOrDefault(e => e.PlcTag == tag);
}
