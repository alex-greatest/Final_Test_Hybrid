namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги аналоговых датчиков для панели "Входы 2".
/// Адрес: DB_Measure.Sensor
/// </summary>
public static class SensorScreenTags
{
    /// <summary>POG — Gas Flow G20</summary>
    public const string GasPog = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG\"";

    /// <summary>FS — DHW Water Flow</summary>
    public const string DhwFs = "ns=3;s=\"DB_Measure\".\"Sensor\".\"DHW_FS\"";

    /// <summary>FR — CH Water Flow</summary>
    public const string ChFr = "ns=3;s=\"DB_Measure\".\"Sensor\".\"CH_FR\"";

    /// <summary>FA_CO — CO UMAT</summary>
    public const string FaCo = "ns=3;s=\"DB_Measure\".\"Sensor\".\"FA_CO\"";

    /// <summary>FA_CO2 — CO2 UMAT</summary>
    public const string FaCo2 = "ns=3;s=\"DB_Measure\".\"Sensor\".\"FA_CO2\"";

    /// <summary>PAG — Blr Gas Pressure</summary>
    public const string GasPag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PAG\"";

    /// <summary>PES — DHW In Pressure</summary>
    public const string DhwPes = "ns=3;s=\"DB_Measure\".\"Sensor\".\"DHW_PES\"";

    /// <summary>PMR — CH Flow Pressure</summary>
    public const string ChPmr = "ns=3;s=\"DB_Measure\".\"Sensor\".\"CH_PMR\"";

    /// <summary>04B1 — Blr Supply</summary>
    public const string BlrSupply = "ns=3;s=\"DB_Measure\".\"Sensor\".\"BlrSupply\"";

    /// <summary>POG1 — Gas Flow (20B1 RT 24V frm Blr)</summary>
    public const string GasPog1 = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG1\"";

    /// <summary>FA_O2 — O2 UMAT</summary>
    public const string FaO2 = "ns=3;s=\"DB_Measure\".\"Sensor\".\"FA_O2\"";

    /// <summary>Gas_P — Gas Flow Pressure (CO2 Cascade)</summary>
    public const string GasP = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_P\"";

    /// <summary>Gas_Pa — Barometer (RRR CH Rtn Pres)</summary>
    public const string GasPa = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_Pa\"";

    /// <summary>POG1_N — Gas Flow Normalized</summary>
    public const string GasPog1N = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG1_N\"";
}
