namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги аналоговых датчиков для компонента AiCallCheck.
/// Адрес: DB_Sensor
/// </summary>
public static class AiCallCheckTags
{
    private const string BaseAddress = "ns=3;s=\"DB_Sensor\"";

    /// <summary>
    /// Список всех датчиков для AiCallCheck.
    /// </summary>
    public static readonly IReadOnlyList<SensorInfo> Sensors =
    [
        new("Gas_POG", "Расход газа G20 (0-100)"),
        new("Gas_PAG", "Давление газа на входе (0-60)"),
        new("Gas_PGB", "Давление газа на горелке (0-60)"),
        new("CH_FR", "Расход воды CH (0-25)"),
        new("CH_PMR", "Давление воды CH (0-4)"),
        new("DHW_FS", "Расход воды DHW (0-25)"),
        new("DHW_PES", "Давление на входе DHW (0-25)"),
        new("FA_CO", "Уровень CO (0-300)"),
        new("FA_CO2", "Уровень CO2 (0-20)"),
        new("FA_O2", "Уровень O2 (0-24)"),
        new("BlrSupply", "Напряжение питания (0-250)"),
        new("Gas_POG1", "Расход газа 24V"),
        new("Gas_P", "Давление газа"),
        new("Gas_Pa", "Атмосферное давление")
    ];

    /// <summary>
    /// Поля датчика в PLC.
    /// </summary>
    public static class Fields
    {
        public const string Value = "Value";
        public const string ValueAct = "Value_Act";
        public const string LimMin = "Lim_Min";
        public const string LimMax = "Lim_Max";
        public const string Gain = "Gain";
        public const string Offset = "Offset";
    }

    /// <summary>
    /// Формирует NodeId для тега датчика.
    /// </summary>
    /// <param name="sensorName">Имя датчика.</param>
    /// <param name="field">Поле датчика.</param>
    /// <returns>NodeId для OPC-UA.</returns>
    public static string BuildNodeId(string sensorName, string field) =>
        $"{BaseAddress}.\"{sensorName}\".\"{field}\"";
}

/// <summary>
/// Информация о датчике.
/// </summary>
/// <param name="Name">Имя тега в PLC.</param>
/// <param name="Description">Описание датчика.</param>
public record SensorInfo(string Name, string Description);
