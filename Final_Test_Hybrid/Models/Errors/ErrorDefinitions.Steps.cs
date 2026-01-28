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
    
    public static readonly ErrorDefinition PowerCableNotConnected = new(
        "П-009-00", "Не подключен силовой кабель",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-power-cable",
        RelatedStepName: "Elec/Connect_Power_Cable");

    public static readonly ErrorDefinition EarthClipNotConnected = new(
        "П-009-01", "Клипса заземление не подключена",
        Severity: ErrorSeverity.Warning,
        ActivatesResetButton: true,
        RelatedStepId: "elec-connect-earth-clip",
        RelatedStepName: "Elec/Connect_Earth_Clip");
    
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

    public static readonly ErrorDefinition NoDiagnosticConnection = new(
        "П-016-00", "Нет связи с котлом",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-check-comms",
        RelatedStepName: "Coms/Check_Comms");
    
    public static readonly ErrorDefinition WriteBytesOn = new(
        "П-016-01", "Ошибка при смене режима котла",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-write-test-byte-on",
        RelatedStepName: "Coms/Write_Test_Byte_ON");
    
    public static readonly ErrorDefinition BoilerNotStandMode = new(
        "П-016-02", "Котел не в стендовом режиме",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-check-test-byte-on",
        RelatedStepName: "Coms/Check_Test_Byte_ON");

    public static readonly ErrorDefinition EcuWriteError = new(
        "П-016-03", "Ошибка записи в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-write-soft-code-plug",
        RelatedStepName: "Coms/Write_Soft_Code_Plug");

    public static readonly ErrorDefinition ChPumpStartError = new(
        "П-016-04", "Ошибка запуска насоса котла",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-pump-start",
        RelatedStepName: "Coms/CH_Pump_Start");

    public static readonly ErrorDefinition EcuArticleMismatch = new(
        "П-016-05", "Несовпадение артикула в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuBoilerTypeMismatch = new(
        "П-016-06", "Несовпадение типа котла в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpTypeMismatch = new(
        "П-016-07", "Несовпадение типа насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPressureDeviceTypeMismatch = new(
        "П-016-08", "Несовпадение типа датчика давления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuGasRegulatorTypeMismatch = new(
        "П-016-09", "Несовпадение типа регулятора газа в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxChHeatOutputMismatch = new(
        "П-016-10", "Несовпадение макс. теплопроизводительности отопления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxDhwHeatOutputMismatch = new(
        "П-016-11", "Несовпадение макс. теплопроизводительности ГВС в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMinChHeatOutputMismatch = new(
        "П-016-12", "Несовпадение мин. теплопроизводительности отопления в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpModeMismatch = new(
        "П-016-13", "Несовпадение режима работы насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuPumpPowerMismatch = new(
        "П-016-14", "Несовпадение установленной мощности насоса в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuGasTypeMismatch = new(
        "П-016-15", "Несовпадение вида газа в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuCurrentOffsetMismatch = new(
        "П-016-16", "Несовпадение сдвига тока на модуляционной катушке в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuFlowCoefficientMismatch = new(
        "П-016-17", "Несовпадение коэффициента k расхода воды в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxPumpAutoPowerMismatch = new(
        "П-016-18", "Несовпадение макс. мощности насоса в авто режиме в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMinPumpAutoPowerMismatch = new(
        "П-016-19", "Несовпадение мин. мощности насоса в авто режиме в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuComfortHysteresisMismatch = new(
        "П-016-20", "Несовпадение гистерезиса ГВС в режиме комфорт в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuMaxFlowTemperatureMismatch = new(
        "П-016-21", "Несовпадение макс. температуры подающей линии в ЭБУ",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition ThermostatJumperMissing = new(
        "П-016-22", "Не установлена перемычка термостата",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-soft-code-plug",
        RelatedStepName: "Coms/Read_Soft_Code_Plug");

    public static readonly ErrorDefinition EcuFirmwareVersionMismatch = new(
        "П-016-23", "Неверная версия ПО",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-read-ecu-version",
        RelatedStepName: "Coms/Read_ECU_Version");

    // Check Water Flow (П-029-xx)
    public static readonly ErrorDefinition AlNoWaterFlowCheck = new(
        "П-029-00", "Неисправность. Нет протока воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterFlowMinCheck = new(
        "П-029-01", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterFlowMaxCheck = new(
        "П-029-02", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterPressureLowCheck = new(
        "П-029-03", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    public static readonly ErrorDefinition AlWaterPressureHighCheck = new(
        "П-029-04", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Check_Water_Flow\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-check-water-flow",
        RelatedStepName: "CH/Check_Water_Flow");

    // Get CHW Flow NTC Cold (П-031-xx)
    public static readonly ErrorDefinition AlWaterFlowMinGetChwFlowNtcCold = new(
        "П-031-00", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterFlowMaxGetChwFlowNtcCold = new(
        "П-031-01", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterPressureLowGetChwFlowNtcCold = new(
        "П-031-02", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    public static readonly ErrorDefinition AlWaterPressureHighGetChwFlowNtcCold = new(
        "П-031-03", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Get_CHW_Flow_NTC_Cold\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-get-chw-flow-ntc-cold",
        RelatedStepName: "CH/Get_CHW_Flow_NTC_Cold");

    // CH_Start_Max_Heatout (П-032-xx)
    public static readonly ErrorDefinition AlNoWaterFlowChStartMaxHeatout = new(
        "П-032-00", "Неисправность. Нет протока воды",
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout\".\"Al_NoWaterFlow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-max-heatout",
        RelatedStepName: "Coms/CH_Start_Max_Heatout");

    public static readonly ErrorDefinition AlIonCurrentOutTolChStartMaxHeatout = new(
        "П-032-01", "Неисправность. Ток ионизации вне допуска",
        PlcTag: "ns=3;s=\"DB_Coms\".\"DB_CH_Start_Max_Heatout\".\"Al_IonCurrentOutTol\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "coms-ch-start-max-heatout",
        RelatedStepName: "Coms/CH_Start_Max_Heatout");

    // Compare Flow NTC Temperature Cold (П-030-xx)
    public static readonly ErrorDefinition AlDeltaTempNokCompare = new(
        "П-030-00", "Неисправность. Разность температур вне допуска",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_DeltaTempNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterFlowMinCompare = new(
        "П-030-01", "Неисправность. Слишком малый расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterFlowMin\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterFlowMaxCompare = new(
        "П-030-02", "Неисправность. Слишком большой расход воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterFlowMax\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterPressureLowCompare = new(
        "П-030-03", "Неисправность. Низкое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterPressureLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    public static readonly ErrorDefinition AlWaterPressureHighCompare = new(
        "П-030-04", "Неисправность. Высокое давление воды",
        PlcTag: "ns=3;s=\"DB_CH\".\"DB_CH_Compare_Flow_NTC_Temp_Cold\".\"Al_WaterPressureHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "ch-compare-flow-ntc-temperature-cold",
        RelatedStepName: "CH/Compare_Flow_NTC_Temperature_Cold");

    // Gas/Wait_for_Gas_Flow (П-034-xx)
    public static readonly ErrorDefinition AlGasFlowLow = new(
        "П-034-00", "Неисправность. Низкий расход газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

    public static readonly ErrorDefinition AlGasFlowHigh = new(
        "П-034-01", "Неисправность. Высокий расход газа",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

    public static readonly ErrorDefinition AlNotStendReady = new(
        "П-034-02", "Неисправность. Стенд не готов",
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

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
        AlFillTimeСhSlow,
        NoDiagnosticConnection,
        WriteBytesOn,
        BoilerNotStandMode,
        EcuWriteError,
        ChPumpStartError,
        EcuArticleMismatch,
        EcuBoilerTypeMismatch,
        EcuPumpTypeMismatch,
        EcuPressureDeviceTypeMismatch,
        EcuGasRegulatorTypeMismatch,
        EcuMaxChHeatOutputMismatch,
        EcuMaxDhwHeatOutputMismatch,
        EcuMinChHeatOutputMismatch,
        EcuPumpModeMismatch,
        EcuPumpPowerMismatch,
        EcuGasTypeMismatch,
        EcuCurrentOffsetMismatch,
        EcuFlowCoefficientMismatch,
        EcuMaxPumpAutoPowerMismatch,
        EcuMinPumpAutoPowerMismatch,
        EcuComfortHysteresisMismatch,
        EcuMaxFlowTemperatureMismatch,
        ThermostatJumperMissing,
        EcuFirmwareVersionMismatch,
        AlNoWaterFlowCheck,
        AlWaterFlowMinCheck,
        AlWaterFlowMaxCheck,
        AlWaterPressureLowCheck,
        AlWaterPressureHighCheck,
        AlWaterFlowMinGetChwFlowNtcCold,
        AlWaterFlowMaxGetChwFlowNtcCold,
        AlWaterPressureLowGetChwFlowNtcCold,
        AlWaterPressureHighGetChwFlowNtcCold,
        AlNoWaterFlowChStartMaxHeatout,
        AlIonCurrentOutTolChStartMaxHeatout,
        AlDeltaTempNokCompare,
        AlWaterFlowMinCompare,
        AlWaterFlowMaxCompare,
        AlWaterPressureLowCompare,
        AlWaterPressureHighCompare,
        AlGasFlowLow,
        AlGasFlowHigh,
        AlNotStendReady
    ];
}
