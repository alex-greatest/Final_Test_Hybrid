namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Ошибки шага Block_Boiler_Adapter (П-086-xx)
    public static readonly ErrorDefinition Relay17K4Fault = new(
        "П-086-00", "Реле 17K4 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K4Fault\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block_boiler_adapter",
        RelatedStepName: "Block Boiler Adapter");

    public static readonly ErrorDefinition Relay17K5Fault = new(
        "П-086-01", "Реле 17K5 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K5Fault\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block_boiler_adapter",
        RelatedStepName: "Block Boiler Adapter");

    internal static IEnumerable<ErrorDefinition> StepErrors =>
    [
        Relay17K4Fault,
        Relay17K5Fault
    ];
}
