namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // DB_Message (О-001-xx)
    public static readonly ErrorDefinition Message_ControlNotEnabled = new(
        "О-001-00", "Управление не включено",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[2]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Message_NoMode = new(
        "О-001-01", "Нет режима",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[3]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Message_ModeSelector = new(
        "О-001-02", "Селектор выбора режима",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[4]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Message_ProfibusError = new(
        "О-001-03", "Ошибка Profibus",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[5]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Message_NoAirSupply = new(
        "О-001-04", "Нет подачи воздуха",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[6]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);
    
    public static readonly ErrorDefinition Message_AutomatNotOn = new(
        "О-001-05", "Не включен один из автоматов питания",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[7]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);
    
    public static readonly ErrorDefinition Message_PressButtonStopGas = new(
        "О-001-06", "Нажата кнопка \"Стоп подачи газа\"",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[8]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);
    
    public static readonly ErrorDefinition Message_PressButtonStopAutoCycle = new(
        "О-001-07", "Нажата кнопка \"Выключение автоматического цикла\"",
        PlcTag: "ns=3;s=\"DB_Message\".\"Alarm4\"[9]",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    // DB_Common (О-002-xx)
    public static readonly ErrorDefinition Relay17K4Fault = new(
        "О-002-00", "Реле 17K4 неисправно",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_17K4Fault\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Common_BoilerNotUnlocked = new(
        "О-002-01", "Котел не разблокирован",
        PlcTag: "ns=3;s=\"DB_Common\".\"Al_Not_17K5\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    // DB_Coms (О-003-xx)
    public static readonly ErrorDefinition Coms_NoWaterFlow = new(
        "О-003-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Coms_IonCurrentOutTol = new(
        "О-003-01", "Ток ионизации вне допуска",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_IonCurrentOutTol\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Coms_NotStandReady = new(
        "О-003-02", "Стенд не готов",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition Coms_CloseTimeExceeded = new(
        "О-003-03", "Время закрытия клапана превышено",
        PlcTag: "ns=3;s=\"DB_Coms\".\"Al_CloseTime\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    // DB_Elec (О-005-xx)
    public static readonly ErrorDefinition ElecRelay6K1Fault = new(
        "О-005-00", "Неисправность реле 6K1",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_6K1\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition ElecRelay6K2Fault = new(
        "О-005-01", "Неисправность реле 6K2",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_6K2\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition ElecIsometerFault = new(
        "О-005-02", "Неисправность изоляции",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_Isometer\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition ElecVoltageMinFault = new(
        "О-005-03", "Неисправность. Напряжение меньше допустимого",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_VoltageMin\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition ElecVoltageMaxFault = new(
        "О-005-04", "Неисправность. Напряжение больше допустимого",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_VoltageMax\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    public static readonly ErrorDefinition ElecAdapterNotInFault = new(
        "О-005-05", "Неисправность. Адаптер не вставлен",
        PlcTag: "ns=3;s=\"DB_Elec\".\"Al_AdapterNotIn\"",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true);

    internal static IEnumerable<ErrorDefinition> GlobalPlcErrors =>
    [
        Message_ControlNotEnabled,
        Message_NoMode,
        Message_ModeSelector,
        Message_ProfibusError,
        Message_NoAirSupply,
        Message_AutomatNotOn,
        Message_PressButtonStopGas,
        Message_PressButtonStopAutoCycle,
        Relay17K4Fault,
        Common_BoilerNotUnlocked,
        Coms_NoWaterFlow,
        Coms_IonCurrentOutTol,
        Coms_NotStandReady,
        Coms_CloseTimeExceeded,
        ElecRelay6K1Fault,
        ElecRelay6K2Fault,
        ElecIsometerFault,
        ElecVoltageMinFault,
        ElecVoltageMaxFault,
        ElecAdapterNotInFault
    ];
}
