namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // Ошибки шага Block_Boiler_Adapter (П-086-xx)
    public static readonly ErrorDefinition BoilerNotLocked = new(
        "П-086-00", "Котел не заблокирован",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_Not_17K4\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block-boiler-adapter",
        RelatedStepName: "Block Boiler Adapter");

    public static readonly ErrorDefinition Relay17K5Fault = new(
        "П-086-01", "Реле 17K5 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K5Fault\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block-boiler-adapter",
        RelatedStepName: "Block Boiler Adapter");

    internal static IEnumerable<ErrorDefinition> StepErrors =>
    [
        BoilerNotLocked,
        Relay17K5Fault
    ];
}
