namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Глобальные программные ошибки
    public static readonly ErrorDefinition OpcConnectionLost = new(
        "G010", "Потеря связи с ПЛК",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition DatabaseError = new(
        "G011", "Ошибка базы данных",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition TagReadTimeout = new(
        "G012", "Таймаут чтения тега ПЛК",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition PlcWriteError = new(
        "О-004-00", "Ошибка записи в ПЛК",
        Severity: ErrorSeverity.Critical);

    internal static IEnumerable<ErrorDefinition> GlobalAppErrors =>
    [
        OpcConnectionLost,
        DatabaseError,
        TagReadTimeout,
        PlcWriteError
    ];
}
