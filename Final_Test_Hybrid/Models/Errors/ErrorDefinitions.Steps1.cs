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
        AlDeltaTempNokDhwCompareFlowNtcTempHot
    ];
}
