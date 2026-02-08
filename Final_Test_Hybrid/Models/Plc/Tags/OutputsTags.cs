namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги выходов для панели "Выходы".
/// </summary>
public static class OutputsTags
{
    // EU3 - Lamp Outputs (toggle - один тег для чтения и записи)
    public const string H51_1 = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"51H1\"";
    public const string H30_6 = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"30H6\"";
    public const string H51_2 = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"51H2\"";
    public const string H32_7 = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"32H7\"";

    // HMI Button Control (DB_HMI.Button.SB) - только Set
    public const string K7_1_BoilerOn_Sb = "ns=3;s=\"DB_HMI\".\"Button\".\"SB\".\"7K1\"";
    public const string K7_2_CompOn_Sb = "ns=3;s=\"DB_HMI\".\"Button\".\"SB\".\"7K2\"";
    public const string K7_3_CurTongsOn_Sb = "ns=3;s=\"DB_HMI\".\"Button\".\"SB\".\"7K3\"";
    public const string K7_4_NeitralOn_Sb = "ns=3;s=\"DB_HMI\".\"Button\".\"SB\".\"7K4\"";
    public const string K7_5_SelShukoAdapter_Sb = "ns=3;s=\"DB_HMI\".\"Button\".\"SB\".\"7K5\"";

    // HMI Button Status (DB_HMI.Button.Stat_Sb)
    public const string K7_1_BoilerOn_Stat = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"7K1\"";
    public const string K7_2_CompOn_Stat = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"7K2\"";
    public const string K7_3_CurTongsOn_Stat = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"7K3\"";
    public const string K7_4_NeitralOn_Stat = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"7K4\"";
    public const string K7_5_SelShukoAdapter_Stat = "ns=3;s=\"DB_HMI\".\"Button\".\"Stat_Sb\".\"7K5\"";

    // Pneumatic Valves Status (xOut)
    public const string Ev0_3_BoilerGasOn = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"EV0_3\".\"HMI\".\"xOut_Open\"";
    public const string Ev3_1_AirOn = "ns=3;s=\"DB_PneuValve\".\"EV3_1\".\"AirOn\"";
    public const string Ev3_2_BlockBoiler = "ns=3;s=\"DB_PneuValve\".\"EV3_2\".\"HMI\".\"xOut_Work\"";
    public const string Pump1_1_WaterSupply = "ns=3;s=\"DB_PneuValve\".\"CH\".\"PUMP1_1\".\"HMI\".\"xOut_Open\"";

    // Pneumatic Valves Control (xSB)
    public const string Ev0_3_BoilerGasOn_Sb = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"EV0_3\".\"HMI\".\"xSB_Open\"";
    public const string Ev3_2_BlockBoiler_Sb = "ns=3;s=\"DB_PneuValve\".\"EV3_2\".\"HMI\".\"xSB_Work\"";
    public const string Pump1_1_WaterSupply_Sb = "ns=3;s=\"DB_PneuValve\".\"CH\".\"PUMP1_1\".\"HMI\".\"xSB_Open\"";
}
