namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    #region DHW/Fill_Circuit_Normal_Direction

    public static readonly ErrorDefinition AlNoWaterFlowDhw = new(
        "П-200-00", "Нет протока воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");

    public static readonly ErrorDefinition AlNoWaterPressureDhw = new(
        "П-200-01", "Нет давления воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");

    public static readonly ErrorDefinition AlFillTimeDhw = new(
        "П-200-02", "Время заполнения превышено",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Fill_Circuit_Normal\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-fill-circuit-normal-direction",
        RelatedStepName: "DHW/Fill_Circuit_Normal_Direction");

    #endregion

    #region DHW/Check_Tank_Mode

    public static readonly ErrorDefinition AlWaterFlowLowDhwCheckTank = new(
        "П-201-00", "Неисправность. Низкий расход воды в контуре",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Check_Tank_Mode\".\"Al_WaterFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-tank-mode",
        RelatedStepName: "DHW/Check_Tank_Mode");

    public static readonly ErrorDefinition AlWaterFlowHighDhwCheckTank = new(
        "П-201-01", "Неисправность. Высокий расход воды в контуре",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Check_Tank_Mode\".\"Al_WaterFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-tank-mode",
        RelatedStepName: "DHW/Check_Tank_Mode");

    public static readonly ErrorDefinition AlNoWaterPressureDhwCheckTank = new(
        "П-201-02", "Неисправность. Нет давления воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Check_Tank_Mode\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-tank-mode",
        RelatedStepName: "DHW/Check_Tank_Mode");

    #endregion

    #region DHW/Purge_Circuit_Normal_Direction

    public static readonly ErrorDefinition AlNoStendReadyDhwPurge = new(
        "П-202-00", "Неисправность. Система не готова к продувке",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Purge_Circuit_Normal_Direction\".\"Al_NoStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-purge-circuit-normal-direction",
        RelatedStepName: "DHW/Purge_Circuit_Normal_Direction");

    #endregion

    #region DHW/Purge_Circuit_Reverse_Direction

    public static readonly ErrorDefinition AlNoStendReadyDhwPurgeReverse = new(
        "П-203-00", "Неисправность. Система не готова к продувке",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Purge_Circuit_Reverse_Direction\".\"Al_NoStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-purge-circuit-reverse-direction",
        RelatedStepName: "DHW/Purge_Circuit_Reverse_Direction");

    #endregion

    #region DHW/Reduce_Circuit_Pressure

    public static readonly ErrorDefinition AlFlushTimeDhwReduceCircuit = new(
        "П-204-00", "Неисправность. Время заполнение превышено",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Reduce_Circuit_Pressure\".\"Al_FlushTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-reduce-circuit-pressure",
        RelatedStepName: "DHW/Reduce_Circuit_Pressure");

    #endregion

    #region DHW/Check_Flow_Temperature_Rise

    public static readonly ErrorDefinition AlDeltaTempNokDhwCheckFlowTempRise = new(
        "П-205-00", "Неисправность. Разность температур вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Temperature_Rise\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-temperature-rise",
        RelatedStepName: "DHW/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlNoFlowGasDhwCheckFlowTempRise = new(
        "П-205-01", "Неисправность. Не разжёгся котёл",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Temperature_Rise\".\"Al_NoFlowGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-temperature-rise",
        RelatedStepName: "DHW/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlNoSetFlowDhwCheckFlowTempRise = new(
        "П-205-02", "Неисправность. Заданный расход воды не достигнут",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Temperature_Rise\".\"Al_NoSetFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-temperature-rise",
        RelatedStepName: "DHW/Check_Flow_Temperature_Rise");

    #endregion

    #region DHW/Get_Flow_NTC_Cold

    public static readonly ErrorDefinition AlNotStendReadyGetFlowNtcCold = new(
        "П-206-00", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Get_Flow_NTC_Cold\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-get-flow-ntc-cold",
        RelatedStepName: "DHW/Get_Flow_NTC_Cold");

    #endregion

    #region DHW/Check_Flow_Rate

    public static readonly ErrorDefinition AlNotStendReadyCheckFlowRate = new(
        "П-207-00", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlPressureLowCheckFlowRate = new(
        "П-207-01", "Неисправность. Давление не достигнуто",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlWaterFlowMinCheckFlowRate = new(
        "П-207-02", "Неисправность. Слишком малый расход воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlWaterFlowMaxCheckFlowRate = new(
        "П-207-03", "Неисправность. Слишком большой расход воды",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    #endregion

    #region DHW/Compare_Flow_NTC_Temperature_Hot

    public static readonly ErrorDefinition AlNotStendReadyDhwCompareFlowNtcTempHot = new(
        "П-208-00", "Неисправность. Стенд не готов к тесту",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-hot",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Hot");

    public static readonly ErrorDefinition AlDeltaTempNokDhwCompareFlowNtcTempHot = new(
        "П-208-01", "Неисправность. Разность температур вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-hot",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Hot");

    #endregion

    #region DHW/Check_Water_Flow_When_In_DHW_Mode

    public static readonly ErrorDefinition AlFlowChNokCheckWaterFlowDhwMode = new(
        "П-209-00", "Неисправность. Расход воды в контуре CH выше допустимого",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Water_Flow_when_in_DHW_Mode\".\"Al_FlowCHNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-water-flow-when-in-dhw-mode",
        RelatedStepName: "DHW/Check_Water_Flow_When_In_DHW_Mode");

    #endregion

    #region DHW/High_Pressure_Test

    public static readonly ErrorDefinition AlPressureLowDhwHighPressureTest = new(
        "П-210-00", "DB_DHW_High_Pressure_Test. Неисправность. Давление не достигнуто",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_High_Pressure_Test\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-high-pressure-test",
        RelatedStepName: "DHW/High_Pressure_Test");

    public static readonly ErrorDefinition AlPressureHightDhwHighPressureTest = new(
        "П-210-01", "DB_DHW_High_Pressure_Test. Неисправность. Давление выше заданного",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_High_Pressure_Test\".\"Al_PressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-high-pressure-test",
        RelatedStepName: "DHW/High_Pressure_Test");

    #endregion

    #region DHW/Set_Circuit_Pressure

    public static readonly ErrorDefinition AlSelectDhwSetCircuitPressure = new(
        "П-211-00", "DB_DHW_Set_Circuit_Pressure. Неисправность. Ошибка переключения 3-х ходового клапана",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_SelectDHW\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlWaterFlowLowSetCircuitPressure = new(
        "П-211-01", "DB_DHW_Set_Circuit_Pressure. Неисправность. Низкий расход воды в контуре",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_WaterFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlWaterFlowHightSetCircuitPressure = new(
        "П-211-02", "DB_DHW_Set_Circuit_Pressure. Неисправность. Высокий расход воды в контуре",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_WaterFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlPressureLowSetCircuitPressure = new(
        "П-211-03", "DB_DHW_Set_Circuit_Pressure. Неисправность. Давление не достигнуто",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlPressureHightSetCircuitPressure = new(
        "П-211-04", "DB_DHW_Set_Circuit_Pressure. Неисправность. Давление выше заданного",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_PressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    #endregion

    #region DHW/Compare_Flow_NTC_Temperature_Cold

    public static readonly ErrorDefinition AlNotStendReadyDhwCompareFlowNtcTempCold = new(
        "П-212-00", "DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Cold\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-cold",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlDeltaTempNokDhwCompareFlowNtcTempCold = new(
        "П-212-01", "DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Разность температур вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Cold\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-cold",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Cold");

    #endregion

    #region DHW/Set_Tank_Mode

    public static readonly ErrorDefinition AlWaterFlowLowSetTankMode = new(
        "П-213-00", "DB_Set_Tank_Mode. Неисправность. Низкий расход воды в контуре",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Set_Tank_Mode\".\"Al_WaterFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-tank-mode",
        RelatedStepName: "DHW/Set_Tank_Mode");

    public static readonly ErrorDefinition AlPressureLowSetTankMode = new(
        "П-213-01", "DB_Set_Tank_Mode. Неисправность. Давление не достигнуто",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Set_Tank_Mode\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-tank-mode",
        RelatedStepName: "DHW/Set_Tank_Mode");

    #endregion
}


