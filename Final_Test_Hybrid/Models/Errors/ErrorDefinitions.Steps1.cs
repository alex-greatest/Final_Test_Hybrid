namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    // CH/Check_Flow_Temperature_Rise (П-045-xx)
    public static readonly ErrorDefinition AlNotStendReadyTempRise = new(
        "П-045-00", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlNoDisCoolTempRise = new(
        "П-045-01", "Неисправность. Охлаждение не выкл.",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_NoDisCool\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlGasFlowNokTempRise = new(
        "П-045-02", "Неисправность. Расход газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_GasFlowNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlGasFlowPressureNokTempRise = new(
        "П-045-03", "Неисправность. Давление газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_GasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlBnrGasFlowPressureNokTempRise = new(
        "П-045-04", "Неисправность. Давление на горелке вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_BnrGasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterFlowMinTempRise = new(
        "П-045-05", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterFlowMaxTempRise = new(
        "П-045-06", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterPressureLowTempRise = new(
        "П-045-07", "Неисправность. Низкое давление воды в контуре отопления",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterPressureHighTempRise = new(
        "П-045-08", "Неисправность. Высокое давление воды в контуре отопления",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlFillTimeTempRise = new(
        "П-045-09", "Неисправность. Время заполнение превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlDeltaRiseNokTempRise = new(
        "П-045-10", "Неисправность. Изменение температуры вне заданных пределов",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_DeltaRiseNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    // DHW/Get_Flow_NTC_Cold (П-050-xx)
    public static readonly ErrorDefinition AlNotStendReadyGetFlowNtcCold = new(
        "П-050-00", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Get_Flow_NTC_Cold\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-get-flow-ntc-cold",
        RelatedStepName: "DHW/Get_Flow_NTC_Cold");

    // DHW/Check_Flow_Rate (П-053-xx)
    public static readonly ErrorDefinition AlNotStendReadyCheckFlowRate = new(
        "П-053-00", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlPressureLowCheckFlowRate = new(
        "П-053-01", "Неисправность. Давление не достигнуто",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlWaterFlowMinCheckFlowRate = new(
        "П-053-02", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    public static readonly ErrorDefinition AlWaterFlowMaxCheckFlowRate = new(
        "П-053-03", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Flow_Rate\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-flow-rate",
        RelatedStepName: "DHW/Check_Flow_Rate");

    // DHW/Compare_Flow_NTC_Temperature_Hot (П-055-xx)
    public static readonly ErrorDefinition AlNotStendReadyDhwCompareFlowNtcTempHot = new(
        "П-055-00", "Неисправность. Стенд не готов к тесту",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-hot",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Hot");

    public static readonly ErrorDefinition AlDeltaTempNokDhwCompareFlowNtcTempHot = new(
        "П-055-01", "Неисправность. Разность температур вне допуска",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Hot\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-hot",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Hot");

    // DHW/Check_Water_Flow_when_in_DHW_Mode (П-054-xx)
    public static readonly ErrorDefinition AlFlowChNokCheckWaterFlowDhwMode = new(
        "П-054-00", "Неисправность. Расход воды в контуре CH выше допустимого",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Check_Water_Flow_when_in_DHW_Mode\".\"Al_FlowCHNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-check-water-flow-when-in-dhw-mode",
        RelatedStepName: "DHW/Check_Water_Flow_When_In_DHW_Mode");

    // DHW/High_Pressure_Test (П-015-xx)
    public static readonly ErrorDefinition AlPressureLowDhwHighPressureTest = new(
        "П-015-00", "DB_DHW_High_Pressure_Test. Неисправность. Давление не достигнуто",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_High_Pressure_Test\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-high-pressure-test",
        RelatedStepName: "DHW/High_Pressure_Test");

    public static readonly ErrorDefinition AlPressureHightDhwHighPressureTest = new(
        "П-015-01", "DB_DHW_High_Pressure_Test. Неисправность. Давление выше заданного",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_High_Pressure_Test\".\"Al_PressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-high-pressure-test",
        RelatedStepName: "DHW/High_Pressure_Test");

    // Gas/Set_Gas_and_P_Burner_Min_Levels (П-038-xx)
    public static readonly ErrorDefinition AlGasFlowLowSetGasBurnerMin = new(
        "П-038-00", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Низкий расход газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlGasFlowHightSetGasBurnerMin = new(
        "П-038-01", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Высокий расход газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotStendReadySetGasBurnerMin = new(
        "П-038-02", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotConnectSensorPgbSetGasBurnerMin = new(
        "П-038-03", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Не подключена трубка газового клапана",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_NotConnectSensorPGB\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    // DHW/Set_Circuit_Pressure (П-049-xx)
    public static readonly ErrorDefinition AlSelectDhwSetCircuitPressure = new(
        "П-049-00", "DB_DHW_Set_Circuit_Pressure. Неисправность. Ошибка переключения 3-х ходового клапана",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_SelectDHW\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlWaterFlowLowSetCircuitPressure = new(
        "П-049-01", "DB_DHW_Set_Circuit_Pressure. Неисправность. Низкий расход воды в контуре",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_WaterFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlWaterFlowHightSetCircuitPressure = new(
        "П-049-02", "DB_DHW_Set_Circuit_Pressure. Неисправность. Высокий расход воды в контуре",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_WaterFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlPressureLowSetCircuitPressure = new(
        "П-049-03", "DB_DHW_Set_Circuit_Pressure. Неисправность. Давление не достигнуто",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    public static readonly ErrorDefinition AlPressureHightSetCircuitPressure = new(
        "П-049-04", "DB_DHW_Set_Circuit_Pressure. Неисправность. Давление выше заданного",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Set_Circuit_Pressure\".\"Al_PressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-circuit-pressure",
        RelatedStepName: "DHW/Set_Circuit_Pressure");

    // DHW/Compare_Flow_NTC_Temperature_Cold (П-051-xx)
    public static readonly ErrorDefinition AlNotStendReadyDhwCompareFlowNtcTempCold = new(
        "П-051-00", "DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Cold\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-cold",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlDeltaTempNokDhwCompareFlowNtcTempCold = new(
        "П-051-01", "DB_DHW_Compare_Flow_NTC_Temp_Cold. Неисправность. Разность температур вне допуска",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_DHW_Compare_Flow_NTC_Temp_Cold\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-compare-flow-ntc-temperature-cold",
        RelatedStepName: "DHW/Compare_Flow_NTC_Temperature_Cold");

    // Coms/Safety_Time (П-061-xx)
    public static readonly ErrorDefinition AlNotStendReadySafetyTime = new(
        "П-061-00", "DB_Gas_Safety_Time. Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_Gas\".\"DB_Gas_Safety_Time\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-safety-time",
        RelatedStepName: "Coms/Safety_Time");

    public static readonly ErrorDefinition AlCloseTimeSafetyTime = new(
        "П-061-01", "DB_Gas_Safety_Time. Неисправность. Время закрытия клапана превышено",
        PlcTag: "ns=3;s=\"DB_Gas\".\"DB_Gas_Safety_Time\".\"Al_CloseTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-safety-time",
        RelatedStepName: "Coms/Safety_Time");

    // CH/Close_Circuit_Valve (П-066-xx)
    public static readonly ErrorDefinition AlBlrPumpWorkCloseCircuitValve = new(
        "П-066-00", "DB_CH_Close_Circuit_Valve. Неисправность. Насос котла работает",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Close_Circuit_Valve\".\"Al_BlrPumpWork\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-close-circuit-valve",
        RelatedStepName: "CH/Close_Circuit_Valve");

    // CH/Purge_Circuit_Normal_Direction (П-070-xx)
    public static readonly ErrorDefinition AlNoStendReadyChPurgeNormal = new(
        "П-070-00", "DB_CH_Purge_Circuit_Normal_Direction. Неисправность. Система не готова к продувке",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Purge_Circuit_Normal_Direction\".\"Al_NoStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-purge-circuit-normal-direction",
        RelatedStepName: "CH/Purge_Circuit_Normal_Direction");

    // DHW/Set_Tank_Mode (П-085-xx)
    public static readonly ErrorDefinition AlWaterFlowLowSetTankMode = new(
        "П-085-00", "DB_Set_Tank_Mode. Неисправность. Низкий расход воды в контуре",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Set_Tank_Mode\".\"Al_WaterFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-tank-mode",
        RelatedStepName: "DHW/Set_Tank_Mode");

    public static readonly ErrorDefinition AlPressureLowSetTankMode = new(
        "П-085-01", "DB_Set_Tank_Mode. Неисправность. Давление не достигнуто",
        PlcTag: "ns=3;s=\"DB_DHW\".\"DB_Set_Tank_Mode\".\"Al_PressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "dhw-set-tank-mode",
        RelatedStepName: "DHW/Set_Tank_Mode");

    internal static IEnumerable<ErrorDefinition> Steps1Errors =>
    [
        AlNotStendReadyTempRise,
        AlNoDisCoolTempRise,
        AlGasFlowNokTempRise,
        AlGasFlowPressureNokTempRise,
        AlBnrGasFlowPressureNokTempRise,
        AlWaterFlowMinTempRise,
        AlWaterFlowMaxTempRise,
        AlWaterPressureLowTempRise,
        AlWaterPressureHighTempRise,
        AlFillTimeTempRise,
        AlDeltaRiseNokTempRise,
        AlNotStendReadyGetFlowNtcCold,
        AlNotStendReadyCheckFlowRate,
        AlPressureLowCheckFlowRate,
        AlWaterFlowMinCheckFlowRate,
        AlWaterFlowMaxCheckFlowRate,
        AlNotStendReadyDhwCompareFlowNtcTempHot,
        AlDeltaTempNokDhwCompareFlowNtcTempHot,
        AlFlowChNokCheckWaterFlowDhwMode,
        AlPressureLowDhwHighPressureTest,
        AlPressureHightDhwHighPressureTest,
        AlGasFlowLowSetGasBurnerMin,
        AlGasFlowHightSetGasBurnerMin,
        AlNotStendReadySetGasBurnerMin,
        AlNotConnectSensorPgbSetGasBurnerMin,
        AlSelectDhwSetCircuitPressure,
        AlWaterFlowLowSetCircuitPressure,
        AlWaterFlowHightSetCircuitPressure,
        AlPressureLowSetCircuitPressure,
        AlPressureHightSetCircuitPressure,
        AlNotStendReadyDhwCompareFlowNtcTempCold,
        AlDeltaTempNokDhwCompareFlowNtcTempCold,
        AlNotStendReadySafetyTime,
        AlCloseTimeSafetyTime,
        AlBlrPumpWorkCloseCircuitValve,
        AlNoStendReadyChPurgeNormal,
        AlWaterFlowLowSetTankMode,
        AlPressureLowSetTankMode
    ];
}
