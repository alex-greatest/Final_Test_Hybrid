namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    public static readonly ErrorDefinition PlcWriteError = new(
        "О-004-00", "Ошибка записи в ПЛК",
        Severity: ErrorSeverity.Critical);
    
    // Глобальные программные ошибки
    public static readonly ErrorDefinition OpcConnectionLost = new(
        "О-004-01", "Потеря связи с ПЛК",
        Severity: ErrorSeverity.Critical);
    
    public static readonly ErrorDefinition TagReadTimeout = new(
        "О-004-02", "Таймаут чтения тега ПЛК",
        Severity: ErrorSeverity.Critical);
    
    internal static IEnumerable<ErrorDefinition> GlobalAppErrors =>
    [
        OpcConnectionLost,
        TagReadTimeout,
        PlcWriteError
    ];
}
