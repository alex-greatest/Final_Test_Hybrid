namespace Final_Test_Hybrid.Models.Errors;

public static partial class ErrorDefinitions
{
    #region Gas/Leak_Test

    public static readonly ErrorDefinition AlLeackGas = new(
        "П-400-00", "Утечка газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Leak_Test\".\"Al_LeackGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-leak-test",
        RelatedStepName: "Gas/Leak_Test");

    public static readonly ErrorDefinition AlNoPressureGas = new(
        "П-400-01", "Нет давления газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Leak_Test\".\"Al_NoPressureGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-leak-test",
        RelatedStepName: "Gas/Leak_Test");

    #endregion

    #region Gas/Wait_for_Gas_Flow

    public static readonly ErrorDefinition AlGasFlowLow = new(
        "П-401-00", "Неисправность. Низкий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

    public static readonly ErrorDefinition AlGasFlowHigh = new(
        "П-401-01", "Неисправность. Высокий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

    public static readonly ErrorDefinition AlNotStendReady = new(
        "П-401-02", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Wait_for_Gas_Flow\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-wait-for-gas-flow",
        RelatedStepName: "Gas/Wait_for_Gas_Flow");

    #endregion

    #region Gas/Set_Required_Pressure

    public static readonly ErrorDefinition AlGasFlowLowSetRequiredPressure = new(
        "П-402-00", "Неисправность. Низкий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Required_Pressure\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-required-pressure",
        RelatedStepName: "Gas/Set_Required_Pressure");

    public static readonly ErrorDefinition AlGasFlowHightSetRequiredPressure = new(
        "П-402-01", "Неисправность. Высокий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Required_Pressure\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-required-pressure",
        RelatedStepName: "Gas/Set_Required_Pressure");

    public static readonly ErrorDefinition AlNotStendReadySetRequiredPressure = new(
        "П-402-02", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Required_Pressure\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-required-pressure",
        RelatedStepName: "Gas/Set_Required_Pressure");

    public static readonly ErrorDefinition AlGasPressureNokSetRequiredPressure = new(
        "П-402-03", "Неисправность. Заданное значение давления газа не достигнуто",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Required_Pressure\".\"Al_GasPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-required-pressure",
        RelatedStepName: "Gas/Set_Required_Pressure");

    #endregion

    #region Gas/Set_Gas_and_P_Burner_Max_Levels

    public static readonly ErrorDefinition AlGasFlowLowSetGasBurnerMax = new(
        "П-403-00", "Неисправность. Низкий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Max_Levels\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlGasFlowHighSetGasBurnerMax = new(
        "П-403-01", "Неисправность. Высокий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Max_Levels\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlNotStendReadySetGasBurnerMax = new(
        "П-403-02", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Max_Levels\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlNotConnectSensorPgbSetGasBurnerMax = new(
        "П-403-03", "Неисправность. Не подключена трубка газового клапана",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Max_Levels\".\"Al_NotConnectSensorPGB\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Max_Levels");

    #endregion

    #region Gas/Check_Gas_and_P_Burner_Max_Levels

    public static readonly ErrorDefinition AlGasFlowNokCheckGasBurnerMax = new(
        "П-404-00", "Неисправность. Расход газа вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Max_Levels\".\"Al_GasFlowNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlGasFlowPressureNokCheckGasBurnerMax = new(
        "П-404-01", "Неисправность. Давление газа вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Max_Levels\".\"Al_GasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlBnrGasFlowPressureNokCheckGasBurnerMax = new(
        "П-404-02", "Неисправность. Давление на горелке вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Max_Levels\".\"Al_BnrGasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlNotStendReadyCheckGasBurnerMax = new(
        "П-404-03", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Max_Levels\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Max_Levels");

    public static readonly ErrorDefinition AlNotConnectSensorPgbCheckGasBurnerMax = new(
        "П-404-04", "Неисправность. Не подключена трубка газового клапана",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Max_Levels\".\"Al_NotConnectSensorPGB\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-max-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Max_Levels");

    #endregion

    #region Gas/Check_Gas_and_P_Burner_Min_Levels

    public static readonly ErrorDefinition AlGasFlowNokCheckGasBurnerMin = new(
        "П-405-00", "Неисправность. Расход газа вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlGasFlowPressureNokCheckGasBurnerMin = new(
        "П-405-01", "Неисправность. Давление газа вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlBnrGasFlowPressureNokCheckGasBurnerMin = new(
        "П-405-02", "Неисправность. Давление на горелке вне допуска",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Min_Levels\".\"Al_BnrGasFlowPressureNOK\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotStendReadyCheckGasBurnerMin = new(
        "П-405-03", "Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Min_Levels\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotConnectSensorPgbCheckGasBurnerMin = new(
        "П-405-04", "Неисправность. Не подключена трубка газового клапана",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Auto_Check_Gas_and_P_Burner_Min_Levels\".\"Al_NotConnectSensorPGB\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-check-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Check_Gas_and_P_Burner_Min_Levels");

    #endregion

    #region Gas/Close_Circuit

    public static readonly ErrorDefinition AlLeackGasCloseCircuit = new(
        "П-406-00", "Неисправность. Утечка газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Close_Circuit\".\"Al_LeackGas\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-close-circuit",
        RelatedStepName: "Gas/Close_Circuit");

    #endregion

    #region Gas/Set_Gas_and_P_Burner_Min_Levels

    public static readonly ErrorDefinition AlGasFlowLowSetGasBurnerMin = new(
        "П-407-00", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Низкий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowLow\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlGasFlowHightSetGasBurnerMin = new(
        "П-407-01", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Высокий расход газа",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_GasFlowHight\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotStendReadySetGasBurnerMin = new(
        "П-407-02", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Стенд не готов",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_NotStendReady\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    public static readonly ErrorDefinition AlNotConnectSensorPgbSetGasBurnerMin = new(
        "П-407-03", "Gas_Set_Gas_and_P_Burner_Min_Levels. Неисправность. Не подключена трубка газового клапана",
        ActivatesResetButton: true,
        PlcTag: "ns=3;s=\"DB_Gas\".\"Gas_Set_Gas_and_P_Burner_Min_Levels\".\"Al_NotConnectSensorPGB\"",
        Severity: ErrorSeverity.Critical,
        RelatedStepId: "gas-set-gas-and-p-burner-min-levels",
        RelatedStepName: "Gas/Set_Gas_and_P_Burner_Min_Levels");

    #endregion
}


