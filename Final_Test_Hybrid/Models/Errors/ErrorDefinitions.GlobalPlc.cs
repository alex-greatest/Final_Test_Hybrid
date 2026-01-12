namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // DB_Message (О-001-xx)
    public static readonly ErrorDefinition Message_ControlNotEnabled = new(
        "О-001-00", "Управление не включено",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[2]",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Message_NoMode = new(
        "О-001-01", "Нет режима",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[3]",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Message_ModeSelector = new(
        "О-001-02", "Селектор выбора режима",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[4]",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Message_ProfibusError = new(
        "О-001-03", "Ошибка Profibus",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[5]",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Message_NoAirSupply = new(
        "О-001-04", "Нет подачи воздуха",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[6]",
        Severity: ErrorSeverity.Critical);

    // DB_Common (О-002-xx)
    public static readonly ErrorDefinition Relay17K4Fault = new(
        "О-002-00", "Реле 17K4 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K4Fault\"",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Common_BoilerNotUnlocked = new(
        "О-002-01", "Котел не разблокирован",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_Not_17K5\"",
        Severity: ErrorSeverity.Critical);

    // DB_Coms (О-003-xx)
    public static readonly ErrorDefinition Coms_NoWaterFlow = new(
        "О-003-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Coms_IonCurrentOutTol = new(
        "О-003-01", "Ток ионизации вне допуска",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_IonCurrentOutTol\"",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Coms_NotStandReady = new(
        "О-003-02", "Стенд не готов",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical);

    public static readonly ErrorDefinition Coms_CloseTimeExceeded = new(
        "О-003-03", "Время закрытия клапана превышено",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_CloseTime\"",
        Severity: ErrorSeverity.Critical);

    internal static IEnumerable<ErrorDefinition> GlobalPlcErrors =>
    [
        Message_ControlNotEnabled,
        Message_NoMode,
        Message_ModeSelector,
        Message_ProfibusError,
        Message_NoAirSupply,
        Relay17K4Fault,
        Common_BoilerNotUnlocked,
        Coms_NoWaterFlow,
        Coms_IonCurrentOutTol,
        Coms_NotStandReady,
        Coms_CloseTimeExceeded
    ];
}
