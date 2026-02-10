namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    public static readonly ErrorDefinition PlcWriteError = new(
        "О-004-00", "Ошибка записи в ПЛК",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical);
    
    // Глобальные программные ошибки
    public static readonly ErrorDefinition OpcConnectionLost = new(
        "О-004-01", "Потеря связи с ПЛК",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical);
    
    public static readonly ErrorDefinition TagReadTimeout = new(
        "О-004-02", "Таймаут чтения тега ПЛК",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical);
    
    internal static IEnumerable<ErrorDefinition> GlobalAppErrors =>
    [
        OpcConnectionLost,
        TagReadTimeout,
        PlcWriteError
    ];
}
