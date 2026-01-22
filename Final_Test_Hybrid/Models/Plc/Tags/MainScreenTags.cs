namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги для отображения показаний датчиков на главном экране.
/// </summary>
public static class MainScreenTags
{
    #region DB_Measure.Sensor

    /// <summary>Расход газа G20 (Q-Gas)</summary>
    public const string GasPog = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG\"";

    /// <summary>Давление газа (P-Gas)</summary>
    public const string GasPag = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PAG\"";

    /// <summary>Давление горелки (Burner)</summary>
    public const string GasPgb = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_PGB\"";

    /// <summary>Расход ГВС (Q-DHW)</summary>
    public const string DhwFs = "ns=3;s=\"DB_Measure\".\"Sensor\".\"DHW_FS\"";

    /// <summary>Давление ГВС (P-DHW)</summary>
    public const string DhwPes = "ns=3;s=\"DB_Measure\".\"Sensor\".\"DHW_PES\"";

    /// <summary>Расход отопления (Q-CH)</summary>
    public const string ChFr = "ns=3;s=\"DB_Measure\".\"Sensor\".\"CH_FR\"";

    /// <summary>Давление отопления (P-CH)</summary>
    public const string ChPmr = "ns=3;s=\"DB_Measure\".\"Sensor\".\"CH_PMR\"";

    /// <summary>Концентрация CO</summary>
    public const string FaCo = "ns=3;s=\"DB_Measure\".\"Sensor\".\"FA_CO\"";

    /// <summary>Концентрация CO2</summary>
    public const string FaCo2 = "ns=3;s=\"DB_Measure\".\"Sensor\".\"FA_CO2\"";

    /// <summary>Расход газа POG1</summary>
    public const string GasPog1 = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_POG1\"";

    /// <summary>Атмосферное давление (P атм)</summary>
    public const string GasPa = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_Pa\"";

    /// <summary>Давление газа (Gas_P)</summary>
    public const string GasP = "ns=3;s=\"DB_Measure\".\"Sensor\".\"Gas_P\"";

    #endregion

    #region DB_Measure.Temper

    /// <summary>Температура газа (T-Gas)</summary>
    public const string GasTag = "ns=3;s=\"DB_Measure\".\"Temper\".\"GAS_TAG\"";

    /// <summary>Температура ГВС на входе (T-DHW IN)</summary>
    public const string DhwTes = "ns=3;s=\"DB_Measure\".\"Temper\".\"DHW_TES\"";

    /// <summary>Температура ГВС на выходе (T-DHW OUT)</summary>
    public const string DhwTus = "ns=3;s=\"DB_Measure\".\"Temper\".\"DHW_TUS\"";

    /// <summary>Температура обратки отопления (T-CH RTN)</summary>
    public const string ChTrr = "ns=3;s=\"DB_Measure\".\"Temper\".\"CH_TRR\"";

    /// <summary>Температура подачи отопления (T-CH FLOW)</summary>
    public const string ChTmr = "ns=3;s=\"DB_Measure\".\"Temper\".\"CH_TMR\"";

    #endregion

    #region DB_Parameter

    /// <summary>Дельта температуры отопления</summary>
    public const string ChDeltaTemp = "ns=3;s=\"DB_Parameter\".\"CH\".\"DeltaTemp\"";

    /// <summary>Дельта температуры ГВС</summary>
    public const string DhwDeltaTemp = "ns=3;s=\"DB_Parameter\".\"DHW\".\"DeltaTemp\"";

    #endregion
}
