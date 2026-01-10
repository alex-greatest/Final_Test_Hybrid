namespace Final_Test_Hybrid.Models.Errors;

public static class ErrorDefinitions
{
    // Глобальные ПЛК ошибки
    public static readonly ErrorDefinition EmergencyStop = new(
        "G001", "Аварийная остановка",
        PlcTag: "ns=3;s=\"DB_Safety\".\"EStop\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

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

    // Ошибки шагов (контекст шага добавляется при вызове)
    public static readonly ErrorDefinition StepTimeout = new(
        "S100", "Таймаут шага",
        Severity: ErrorSeverity.Warning,
        PossibleStepIds: []);

    public static readonly ErrorDefinition StepExecutionFailed = new(
        "S101", "Ошибка выполнения шага",
        Severity: ErrorSeverity.Warning,
        PossibleStepIds: []);

    // Хелперы
    public static IReadOnlyList<ErrorDefinition> All =>
    [
        EmergencyStop, OpcConnectionLost, DatabaseError, TagReadTimeout,
        StepTimeout, StepExecutionFailed
    ];

    public static IEnumerable<ErrorDefinition> PlcErrors => All.Where(e => e.IsPlcBound);
    public static ErrorDefinition? ByCode(string code) => All.FirstOrDefault(e => e.Code == code);
    public static ErrorDefinition? ByPlcTag(string tag) => All.FirstOrDefault(e => e.PlcTag == tag);
}
