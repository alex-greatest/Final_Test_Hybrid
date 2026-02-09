namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    #region Block Boiler Adapter

    public static readonly ErrorDefinition BoilerNotLocked = new(
        "П-500-00", "Котел не заблокирован",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_Not_17K4\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block-boiler-adapter",
        RelatedStepName: "Block Boiler Adapter");

    public static readonly ErrorDefinition Relay17K5Fault = new(
        "П-500-01", "Реле 17K5 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K5Fault\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "block-boiler-adapter",
        RelatedStepName: "Block Boiler Adapter");

    #endregion

    #region Elec/Connect_Power_Cable

    public static readonly ErrorDefinition PowerCableNotConnected = new(
        "П-501-00", "Не подключен силовой кабель",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-power-cable",
        RelatedStepName: "Elec/Connect_Power_Cable");

    #endregion

    #region Elec/Connect_Earth_Clip

    public static readonly ErrorDefinition EarthClipNotConnected = new(
        "П-502-00", "Клипса заземление не подключена",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-earth-clip",
        RelatedStepName: "Elec/Connect_Earth_Clip");

    #endregion
}


