namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги входов для панели "Входы 1".
/// </summary>
public static class InputsTags
{
    // EU1 - Control Tags
    public const string PcOn = "ns=3;s=\"PC_ON\"";
    public const string K6_1_Nok = "ns=3;s=\"6K1_NOK\"";
    public const string K6_1_Ok = "ns=3;s=\"6K1_OK\"";
    public const string K6_2_Nok = "ns=3;s=\"6K2_NOK\"";
    public const string K6_2_Ok = "ns=3;s=\"6K2_OK\"";
    public const string PlugIns = "ns=3;s=\"Plug_Ins\"";
    public const string K7_3_On = "ns=3;s=\"7K3_ON\"";
    public const string IsometerAlarm = "ns=3;s=\"Isometer_Alarm\"";

    // EU2 - Gas Analyzer
    public const string FaAlarmCondensat = "ns=3;s=\"FA_AlarmCondensat\"";
    public const string FaAlarmHeatLine = "ns=3;s=\"FA_AlarmHeatLine\"";
    public const string FaAlarmGasCooler = "ns=3;s=\"FA_AlarmGasCooler\"";
    public const string FaTrip = "ns=3;s=\"FA_Trip\"";
    public const string FaNoReady = "ns=3;s=\"FA_NoReady\"";
    public const string E14_1 = "ns=3;s=\"E14_1\"";
    public const string FaRangeO2 = "ns=3;s=\"FA_RangeO2\"";
    public const string AdapterInserted = "ns=3;s=\"Adapter_Inserted\"";

    // EU1 - Extended
    public const string TcabinetOk = "ns=3;s=\"TCabinetOK\"";
    public const string AirLevelOk = "ns=3;s=\"AlrLevel_OK\"";
    public const string TripOk = "ns=3;s=\"Trip_OK\"";
    public const string SchukoInside = "ns=3;s=\"Schuko_Inside\"";
    public const string FaReqMaintenance = "ns=3;s=\"FA_ReqMaintenance\"";
    public const string FaRangeCo = "ns=3;s=\"FA_RangeCO\"";
    public const string FaRangeCo2 = "ns=3;s=\"FA_RangeCO2\"";
    public const string FaCalUltramat23 = "ns=3;s=\"FA_CalUltramat23\"";

    // EU2 - Extended
    public const string AdapterNotInserted = "ns=3;s=\"Adapter_NotInserted\"";
    public const string BoilerBlocage = "ns=3;s=\"Boiler_Blocage\"";
    public const string BoilerDeBlocage = "ns=3;s=\"Boiler_DeBlocage\"";
    public const string PlateIn = "ns=3;s=\"PlateIn\"";

    // EU3 - Buttons
    public const string E56_0 = "ns=3;s=\"E56_0\"";
    public const string Sb30_2 = "ns=3;s=\"30SB2\"";
    public const string Sb30_13 = "ns=3;s=\"30SB13\"";
    public const string Sb30_3 = "ns=3;s=\"30SB3\"";
    public const string Sb30_4 = "ns=3;s=\"30SB4\"";
    public const string E57_1 = "ns=3;s=\"E57_1\"";
    public const string Sb30_5 = "ns=3;s=\"30SB5\"";
    public const string Sb30_6 = "ns=3;s=\"30SB6\"";
    public const string Sa30_7_1 = "ns=3;s=\"30SA7_1\"";
    public const string E58_1 = "ns=3;s=\"E58_1\"";
    public const string Sa30_7_2 = "ns=3;s=\"30SA7_2\"";
    public const string Sa30_9 = "ns=3;s=\"30SA9\"";
    public const string Sb30_10 = "ns=3;s=\"30SB10\"";
    public const string E59_1 = "ns=3;s=\"E59_1\"";
    public const string E59_2 = "ns=3;s=\"E59_2\"";
    public const string Sb30_11 = "ns=3;s=\"30SB11\"";
    public const string E60_0 = "ns=3;s=\"E60_0\"";
    public const string E60_1 = "ns=3;s=\"E60_1\"";
    public const string S32_4 = "ns=3;s=\"32S4\"";
    public const string S32_3 = "ns=3;s=\"32S3\"";
    public const string S32_5 = "ns=3;s=\"32S5\"";
    public const string S32_6 = "ns=3;s=\"32S6\"";
    public const string S32_7 = "ns=3;s=\"32S7\"";

    // UI - Extended
    public const string Psw3_1 = "ns=3;s=\"PSW3_1\"";
    public const string E64_1 = "ns=3;s=\"E64_1\"";
    public const string S28_2 = "ns=3;s=\"28S2\"";
    public const string E64_3 = "ns=3;s=\"E64_3\"";
    public const string S28_1 = "ns=3;s=\"28S1\"";

    /// <summary>
    /// Формирует Node ID из имени тега.
    /// </summary>
    public static string BuildNodeId(string tagName) => $"ns=3;s=\"{tagName}\"";
}
