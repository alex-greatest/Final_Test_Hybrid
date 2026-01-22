namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги температурных датчиков для панели "RTD входы".
/// Адрес: DB_Measure.Temper
/// </summary>
public static class RtdScreenTags
{
    /// <summary>TAG — Температура входящего газа</summary>
    public const string GasTag = "ns=3;s=\"DB_Measure\".\"Temper\".\"GAS_TAG\"";

    /// <summary>TES — Температура воды на входе ГВ</summary>
    public const string DhwTes = "ns=3;s=\"DB_Measure\".\"Temper\".\"DHW_TES\"";

    /// <summary>TUS — Температура воды на выходе ГВ</summary>
    public const string DhwTus = "ns=3;s=\"DB_Measure\".\"Temper\".\"DHW_TUS\"";

    /// <summary>TRR — Температура возврата отопления</summary>
    public const string ChTrr = "ns=3;s=\"DB_Measure\".\"Temper\".\"CH_TRR\"";

    /// <summary>TMR — Температура подачи отопления</summary>
    public const string ChTmr = "ns=3;s=\"DB_Measure\".\"Temper\".\"CH_TMR\"";
}
