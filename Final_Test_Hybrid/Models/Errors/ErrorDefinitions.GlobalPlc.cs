namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Глобальные ПЛК ошибки
    public static readonly ErrorDefinition EmergencyStop = new(
        "G001", "Аварийная остановка",
        PlcTag: "ns=3;s=\"DB_Safety\".\"EStop\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    internal static IEnumerable<ErrorDefinition> GlobalPlcErrors => [];
}
