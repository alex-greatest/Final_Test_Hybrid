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
    
    public static readonly ErrorDefinition AlNoWaterFlowDhw = new(
        "П-008-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");
    
    public static readonly ErrorDefinition AlNoWaterPressureDhw = new(
        "П-008-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");
    
    public static readonly ErrorDefinition AlFillTimeDhw = new(
        "П-008-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");

    public static readonly ErrorDefinition EarthClipNotConnected = new(
        "П-009-01", "Клипса заземление не подключена",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-earth-clip",
        RelatedStepName: "Elec/Connect_Earth_Clip");

    public static readonly ErrorDefinition PowerCableNotConnected = new(
        "П-009-02", "Присоедините силовой кабель",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-power-cable",
        RelatedStepName: "Elec/Connect_Power_Cable");
    
    public static readonly ErrorDefinition AlLeackGas = new(
        "П-010-00", "Утечка газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Leak_Test\".\"Al_LeackGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-leak-test",
        RelatedStepName: "Gas/Leak_Test");
    
    public static readonly ErrorDefinition AlNoPressureGas = new(
        "П-010-01", "Нет давления газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Leak_Test\".\"Al_NoPressureGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-leak-test",
        RelatedStepName: "Gas/Leak_Test");
    
    public static readonly ErrorDefinition AlNoWaterFlowCh = new(
        "П-011-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");
    
    public static readonly ErrorDefinition AlNoWaterPressureСh = new(
        "П-011-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");
    
    public static readonly ErrorDefinition AlFillTimeСh = new(
        "П-011-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");
    
    public static readonly ErrorDefinition AlNoWaterFlowChSlow = new(
        "П-013-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");
    
    public static readonly ErrorDefinition AlNoWaterPressureСhSlow = new(
        "П-013-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");
    
    public static readonly ErrorDefinition AlFillTimeСhSlow = new(
        "П-013-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");
    

    internal static IEnumerable<ErrorDefinition> StepErrors =>
    [
        BoilerNotLocked,
        Relay17K5Fault,
        AlNoWaterFlowDhw,
        AlNoWaterPressureDhw,
        AlFillTimeDhw,
        EarthClipNotConnected,
        PowerCableNotConnected,
        AlLeackGas,
        AlNoPressureGas,
        AlNoWaterFlowCh,
        AlNoWaterPressureСh,
        AlFillTimeСh,
        AlNoWaterFlowChSlow,
        AlNoWaterPressureСhSlow,
        AlFillTimeСhSlow
    ];
}
