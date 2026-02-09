namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    #region CH/Fast_Fill_Circuit

    public static readonly ErrorDefinition AlNoWaterFlowCh = new(
        "П-300-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");

    public static readonly ErrorDefinition AlNoWaterPressureСh = new(
        "П-300-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");

    public static readonly ErrorDefinition AlFillTimeСh = new(
        "П-300-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Fast_Fill_Circuit\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-fast-fill-circuit",
        RelatedStepName: "CH/Fast_Fill_Circuit");

    #endregion

    #region CH/Slow_Fill_Circuit

    public static readonly ErrorDefinition AlNoWaterFlowChSlow = new(
        "П-301-00", "Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");

    public static readonly ErrorDefinition AlNoWaterPressureСhSlow = new(
        "П-301-01", "Нет давления воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_NoWaterPressure\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");

    public static readonly ErrorDefinition AlFillTimeСhSlow = new(
        "П-301-02", "Время заполнения превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Slow_Fill_Circuit\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-slow-fill-circuit",
        RelatedStepName: "CH/Slow_Fill_Circuit");

    #endregion

    #region CH/Check_Water_Flow

    public static readonly ErrorDefinition AlNoWaterFlowCheck = new(
        "П-302-00", "Неисправность. Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterFlowMinCheck = new(
        "П-302-01", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterFlowMaxCheck = new(
        "П-302-02", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterPressureLowCheck = new(
        "П-302-03", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterPressureHighCheck = new(
        "П-302-04", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    #endregion

    #region CH/Get_CHW_Flow_NTC_Cold

    public static readonly ErrorDefinition AlWaterFlowMinGetChwFlowNtcCold = new(
        "П-303-00", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterFlowMaxGetChwFlowNtcCold = new(
        "П-303-01", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterPressureLowGetChwFlowNtcCold = new(
        "П-303-02", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterPressureHighGetChwFlowNtcCold = new(
        "П-303-03", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    #endregion

    #region CH/Compare_Flow_NTC_Temperature_Cold

    public static readonly ErrorDefinition AlDeltaTempNokCompare = new(
        "П-304-00", "Неисправность. Разность температур вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterFlowMinCompare = new(
        "П-304-01", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterFlowMaxCompare = new(
        "П-304-02", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterPressureLowCompare = new(
        "П-304-03", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterPressureHighCompare = new(
        "П-304-04", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    #endregion

    #region CH/Compare_Flow_NTC_Temperatures_Hot

    public static readonly ErrorDefinition AlNotStendReadyCompareHot = new(
        "П-305-00", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlGasFlowNokCompareHot = new(
        "П-305-01", "Неисправность. Расход газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_GasFlowNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlGasFlowPressureNokCompareHot = new(
        "П-305-02", "Неисправность. Давление газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_GasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlBnrGasFlowPressureNokCompareHot = new(
        "П-305-03", "Неисправность. Давление на горелке вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_BnrGasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlWaterFlowMinCompareHot = new(
        "П-305-04", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlWaterFlowMaxCompareHot = new(
        "П-305-05", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlWaterPressureLowCompareHot = new(
        "П-305-06", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlWaterPressureHighCompareHot = new(
        "П-305-07", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlFillTimeCompareHot = new(
        "П-305-08", "Неисправность. Время заполнение превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    public static readonly ErrorDefinition AlDeltaTempNokCompareHot = new(
        "П-305-09", "Неисправность. Разность температур вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Hot\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperatures-hot",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperatures_Hot");

    #endregion

    #region CH/Purge_Circuit_Reverse_Direction

    public static readonly ErrorDefinition AlNoStendReadyChPurgeReverse = new(
        "П-306-00", "Неисправность. Система не готова к продувке",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Purge_Circuit_Reverse_Direction\".\"Al_NoStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-purge-circuit-reverse-direction",
        RelatedStepName: "CH/Purge_Circuit_Reverse_Direction");

    #endregion

    #region CH/Check_Flow_Temperature_Rise

    public static readonly ErrorDefinition AlNotStendReadyTempRise = new(
        "П-307-00", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlNoDisCoolTempRise = new(
        "П-307-01", "Неисправность. Охлаждение не выкл.",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_NoDisCool\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlGasFlowNokTempRise = new(
        "П-307-02", "Неисправность. Расход газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_GasFlowNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlGasFlowPressureNokTempRise = new(
        "П-307-03", "Неисправность. Давление газа вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_GasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlBnrGasFlowPressureNokTempRise = new(
        "П-307-04", "Неисправность. Давление на горелке вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_BnrGasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterFlowMinTempRise = new(
        "П-307-05", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterFlowMaxTempRise = new(
        "П-307-06", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterPressureLowTempRise = new(
        "П-307-07", "Неисправность. Низкое давление воды в контуре отопления",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlWaterPressureHighTempRise = new(
        "П-307-08", "Неисправность. Высокое давление воды в контуре отопления",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlFillTimeTempRise = new(
        "П-307-09", "Неисправность. Время заполнение превышено",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_FillTime\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    public static readonly ErrorDefinition AlDeltaRiseNokTempRise = new(
        "П-307-10", "Неисправность. Изменение температуры вне заданных пределов",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Flow_Temperature_Rise\".\"Al_DeltaRiseNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-flow-temperature-rise",
        RelatedStepName: "CH/Check_Flow_Temperature_Rise");

    #endregion

    #region CH/Close_Circuit_Valve

    public static readonly ErrorDefinition AlBlrPumpWorkCloseCircuitValve = new(
        "П-308-00", "DB_CH_Close_Circuit_Valve. Неисправность. Насос котла работает",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Close_Circuit_Valve\".\"Al_BlrPumpWork\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-close-circuit-valve",
        RelatedStepName: "CH/Close_Circuit_Valve");

    #endregion

    #region CH/Purge_Circuit_Normal_Direction

    public static readonly ErrorDefinition AlNoStendReadyChPurgeNormal = new(
        "П-309-00", "DB_CH_Purge_Circuit_Normal_Direction. Неисправность. Система не готова к продувке",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Purge_Circuit_Normal_Direction\".\"Al_NoStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-purge-circuit-normal-direction",
        RelatedStepName: "CH/Purge_Circuit_Normal_Direction");

    #endregion
}


