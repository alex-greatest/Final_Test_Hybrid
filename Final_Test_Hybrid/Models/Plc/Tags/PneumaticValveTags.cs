namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги пневматических клапанов DB_PneuValve.
/// </summary>
public static class PneumaticValveTags
{
    // VP1 - CH (Central Heating / Система отопления)
    public const string Vp1_1_Purge = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_1\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_2_Purge = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_2\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_3_Fill = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_3\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_4_FastFill = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_4\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_5_WaterToCh = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_5\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_6_WaterToCh = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_6\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_7_HotWaterNoPump = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_7\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_8_Drain = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_8\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_9_Reserve = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_9\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_10_HotWaterWithPump = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_10\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_11_Drain = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_11\".\"HMI\".\"xOut_Open\"";
    public const string Vp1_12_SlowFill = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_12\".\"HMI\".\"xOut_Open\"";

    // VP2 - DHW (Domestic Hot Water / Горячее водоснабжение)
    public const string Vp2_1_Purge = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_1\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_2_Purge = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_2\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_3_HighPressure = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_3\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_4_Fill = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_4\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_5_BoilerOut = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_5\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_6_Drain = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_6\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_7_BoilerIn = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_7\".\"HMI\".\"xOut_Open\"";
    public const string Vp2_8_Drain = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_8\".\"HMI\".\"xOut_Open\"";

    // VP3 - DHW дополнительные
    public const string Vp3_1 = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP3_1\".\"HMI\".\"xOut_Open\"";

    // VP0 - GAS (Газ)
    public const string Vp0_1_GasG20 = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_1\".\"HMI\".\"xOut_Open\"";
    public const string Vp0_2_GasG25 = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_2\".\"HMI\".\"xOut_Open\"";
    public const string Vp0_3_LpgG30 = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_3\".\"HMI\".\"xOut_Open\"";
    public const string Ev0_3_Gas = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"EV0_3\".\"HMI\".\"xOut_Open\"";

    // VP0 - GAS (Кнопки управления)
    public const string Vp0_1_GasG20_SbOpen = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_1\".\"HMI\".\"xSB_Open\"";
    public const string Vp0_2_GasG25_SbOpen = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_2\".\"HMI\".\"xSB_Open\"";
    public const string Vp0_3_LpgG30_SbOpen = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"VP0_3\".\"HMI\".\"xSB_Open\"";
    public const string Ev0_3_Gas_SbOpen = "ns=3;s=\"DB_PneuValve\".\"Gas\".\"EV0_3\".\"HMI\".\"xSB_Open\"";

    // VP1 - CH (Кнопки управления)
    public const string Vp1_1_Purge_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_1\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_2_Purge_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_2\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_3_Fill_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_3\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_4_FastFill_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_4\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_5_WaterToCh_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_5\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_6_WaterToCh_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_6\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_7_HotWaterNoPump_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_7\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_8_Drain_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_8\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_9_Reserve_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_9\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_10_HotWaterWithPump_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_10\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_11_Drain_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_11\".\"HMI\".\"xSB_Open\"";
    public const string Vp1_12_SlowFill_SbOpen = "ns=3;s=\"DB_PneuValve\".\"CH\".\"VP1_12\".\"HMI\".\"xSB_Open\"";

    // VP2 - DHW (Кнопки управления)
    public const string Vp2_1_Purge_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_1\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_2_Purge_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_2\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_3_HighPressure_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_3\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_4_Fill_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_4\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_5_BoilerOut_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_5\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_6_Drain_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_6\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_7_BoilerIn_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_7\".\"HMI\".\"xSB_Open\"";
    public const string Vp2_8_Drain_SbOpen = "ns=3;s=\"DB_PneuValve\".\"DHW\".\"VP2_8\".\"HMI\".\"xSB_Open\"";
}
