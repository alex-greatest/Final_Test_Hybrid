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
    
    public static readonly ErrorDefinition AlNoWaterFlow = new(
        "П-008-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-flush-circuit-normal-direction",
        RelatedStepName: "DHW/Flush_DHW_Circuit_Normal_Direction");
    
    public static readonly ErrorDefinition AlNoWaterPressure = new(
        "П-008-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-flush-circuit-normal-direction",
        RelatedStepName: "DHW/Flush_DHW_Circuit_Normal_Direction");
    
    public static readonly ErrorDefinition AlFillTime = new(
        "П-008-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-flush-circuit-normal-direction",
        RelatedStepName: "DHW/Flush_DHW_Circuit_Normal_Direction");

    public static readonly ErrorDefinition EarthClipNotConnected = new(
        "П-009-01", "Клипса заземление не подключена",
        Severity: ErrorSeverity.Warning,
        RelatedStepId: "elec-connect-earth-clip",
        RelatedStepName: "Elec/Connect_Earth_Clip");

    internal static IEnumerable<ErrorDefinition> StepErrors =>
    [
        BoilerNotLocked,
        Relay17K5Fault,
        AlNoWaterFlow,
        AlNoWaterPressure,
        AlFillTime,
        EarthClipNotConnected
    ];
}
