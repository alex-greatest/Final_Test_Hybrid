namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Ошибки шагов (контекст шага добавляется при вызове)
    public static readonly ErrorDefinition StepTimeout = new(
        "S100", "Таймаут шага",
        Severity: ErrorSeverity.Warning);

    public static readonly ErrorDefinition StepExecutionFailed = new(
        "S101", "Ошибка выполнения шага",
        Severity: ErrorSeverity.Warning);

    internal static IEnumerable<ErrorDefinition> StepErrors =>
        [];
}
