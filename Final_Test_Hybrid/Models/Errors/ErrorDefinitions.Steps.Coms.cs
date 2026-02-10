namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    #region Coms/Check_Comms

    public static readonly ErrorDefinition NoDiagnosticConnection = new(
        "П-100-00", "Нет связи с котлом",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-check-comms",
        RelatedStepName: "Coms/Check_Comms");

    #endregion

    #region Coms/Write_Test_Byte_ON

    public static readonly ErrorDefinition WriteBytesOn = new(
        "П-101-00", "Ошибка при смене режима котла",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-write-test-byte-on",
        RelatedStepName: "Coms/Write_Test_Byte_ON");

    #endregion

    #region Coms/Write_Test_Byte_OFF

    public static readonly ErrorDefinition WriteBytesOff = new(
        "П-102-00", "Ошибка при выходе из режима Стенд",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-write-test-byte-off",
        RelatedStepName: "Coms/Write_Test_Byte_OFF");

    #endregion

    #region Coms/Check_Test_Byte_ON

    public static readonly ErrorDefinition BoilerNotStandMode = new(
        "П-103-00", "Котел не в стендовом режиме",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-check-test-byte-on",
        RelatedStepName: "Coms/Check_Test_Byte_ON");

    #endregion

    #region Coms/Write_Soft_Code_Plug

    public static readonly ErrorDefinition EcuWriteError = new(
        "П-104-00", "Ошибка записи в ЭБУ",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-write-soft-code-plug",
        RelatedStepName: "Coms/Write_Soft_Code_Plug");

    #endregion

    #region Coms/CH_Pump_Start

    public static readonly ErrorDefinition ChPumpStartError = new(
        "П-105-00", "Ошибка запуска насоса котла",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-pump-start",
        RelatedStepName: "Coms/CH_Pump_Start");

    #endregion

    #region Coms/Read_Soft_Code_Plug

    public static readonly ErrorDefinition EcuArticleMismatch = new(
        "П-106-00", "Несовпадение артикула в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuBoilerTypeMismatch = new(
        "П-106-01", "Несовпадение типа котла в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpTypeMismatch = new(
        "П-106-02", "Несовпадение типа насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPressureDeviceTypeMismatch = new(
        "П-106-03", "Несовпадение типа датчика давления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuGasRegulatorTypeMismatch = new(
        "П-106-04", "Несовпадение типа регулятора газа в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxChHeatOutputMismatch = new(
        "П-106-05", "Несовпадение макс. теплопроизводительности отопления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxDhwHeatOutputMismatch = new(
        "П-106-06", "Несовпадение макс. теплопроизводительности ГВС в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMinChHeatOutputMismatch = new(
        "П-106-07", "Несовпадение мин. теплопроизводительности отопления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpModeMismatch = new(
        "П-106-08", "Несовпадение режима работы насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpPowerMismatch = new(
        "П-106-09", "Несовпадение установленной мощности насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuGasTypeMismatch = new(
        "П-106-10", "Несовпадение вида газа в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuCurrentOffsetMismatch = new(
        "П-106-11", "Несовпадение сдвига тока на модуляционной катушке в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuFlowCoefficientMismatch = new(
        "П-106-12", "Несовпадение коэффициента k расхода воды в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxPumpAutoPowerMismatch = new(
        "П-106-13", "Несовпадение макс. мощности насоса в авто режиме в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMinPumpAutoPowerMismatch = new(
        "П-106-14", "Несовпадение мин. мощности насоса в авто режиме в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuComfortHysteresisMismatch = new(
        "П-106-15", "Несовпадение гистерезиса ГВС в режиме комфорт в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxFlowTemperatureMismatch = new(
        "П-106-16", "Несовпадение макс. температуры подающей линии в ЭБУ",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition ThermostatJumperMissing = new(
        "П-106-17", "Не установлена перемычка термостата",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuConnectionTypeMismatch = new(
        "П-106-18", "Несовпадение типа подключения к котлу (1054)",
        Severity: ErrorSeverity.Critical,
        ActivatesResetButton: true,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    #endregion

    #region Coms/Read_ECU_Version

    public static readonly ErrorDefinition EcuFirmwareVersionMismatch = new(
        "П-107-00", "Неверная версия ПО",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-ecu-version",
        RelatedStepName: "Coms/Read_ECU_Version");

    #endregion

    #region Coms/Check_Test_Byte_OFF

    public static readonly ErrorDefinition BoilerStillInStandMode = new(
        "П-108-00", "Котел все еще в режиме Стенд",
        ActivatesResetButton: true,
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-check-test-byte-off",
        RelatedStepName: "Coms/Check_Test_Byte_OFF");

    #endregion

    #region Coms/CH_Start_Max_Heatout

    public static readonly ErrorDefinition AlNoWaterFlowChStartMaxHeatout = new(
        "П-109-00", "Неисправность. Нет протока воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-max-heatout",
        RelatedStepName: "Coms/CH_Start_Max_Heatout");

    public static readonly ErrorDefinition AlIonCurrentOutTolChStartMaxHeatout = new(
        "П-109-01", "Неисправность. Ток ионизации вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout\".\"Al_IonCurrentOutTol\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-max-heatout",
        RelatedStepName: "Coms/CH_Start_Max_Heatout");

    #endregion

    #region Coms/CH_Start_Min_Heatout

    public static readonly ErrorDefinition AlNoWaterFlowChStartMinHeatout = new(
        "П-110-00", "Неисправность. Нет протока воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Min_Heatout\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-min-heatout",
        RelatedStepName: "Coms/CH_Start_Min_Heatout");

    public static readonly ErrorDefinition AlIonCurrentOutTolChStartMinHeatout = new(
        "П-110-01", "Неисправность. Ток ионизации вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Min_Heatout\".\"Al_IonCurrentOutTol\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-min-heatout",
        RelatedStepName: "Coms/CH_Start_Min_Heatout");

    #endregion

    #region Coms/Safety_Time

    public static readonly ErrorDefinition AlNotStendReadySafetyTime = new(
        "П-111-00", "DB_Gas_Safety_Time. Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"DB_Gas_Safety_Time\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-safety-time",
        RelatedStepName: "Coms/Safety_Time");

    public static readonly ErrorDefinition AlCloseTimeSafetyTime = new(
        "П-111-01", "DB_Gas_Safety_Time. Неисправность. Время закрытия клапана превышено",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"DB_Gas_Safety_Time\".\"Al_CloseTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-safety-time",
        RelatedStepName: "Coms/Safety_Time");

    #endregion
}


