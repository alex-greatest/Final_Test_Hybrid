namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги выходов для панели "Выходы".
/// </summary>
public static class OutputsTags
{
    // EU3 - Lamp Outputs
    public const string H51_1 = "ns=3;s=\"51H1\"";
    public const string H30_6 = "ns=3;s=\"30H6\"";
    public const string H51_2 = "ns=3;s=\"51H2\"";
    public const string H32_7 = "ns=3;s=\"32H7\"";

    // EU1 - Control Outputs
    public const string K7_2_CompOn = "ns=3;s=\"7K2_CompOn\"";
    public const string K7_1_BoilerOn = "ns=3;s=\"7K1_BoilerOn\"";
    public const string K7_4_NeitralOn = "ns=3;s=\"7K4_NeitralOn\"";
    public const string K7_3_CurTongsOn = "ns=3;s=\"7K3_CurTongsOn\"";
    public const string K7_5_SelShukoAdapter = "ns=3;s=\"7K5_SelShukoAdapter\"";

    // Pneumatic Valves (DB_PneuValve)
    public const string Ev0_3_BoilerGasOn = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"EV0_3\".\"HMI\".\"xOut_Open\"";
    public const string Ev3_1_AirOn = "ns=3;s=\"DB_PneuValve\".\"EV3_1\".\"AirOn\"";
    public const string Ev3_2_BlockBoiler = "ns=3;s=\"DB_PneuValve\".\"EV3_2\".\"HMI\".\"xOut_Work\"";
    public const string Pump1_1_WaterSupply = "ns=3;s=\"DB_PneuValve\".\"CH\".\"PUMP1_1\".\"HMI\".\"xOut_Open\"";
}
