namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги температурных датчиков Pt100 для компонента RtdCalCheck.
/// Адрес: DB_Sensor
/// </summary>
public static class RtdCalCheckTags
{
    private const string BaseAddress = "ns=3;s=\"DB_Sensor\"";

    /// <summary>
    /// Список всех датчиков Pt100 для RtdCalCheck.
    /// </summary>
    public static readonly IReadOnlyList<SensorInfo> Sensors =
    [
        new("TAG", "Линия GAS. Температура газа"),
        new("CH_TMR", "Линия CH. Температура потока воды"),
        new("CH_TRR", "Линия CH. Температура обратной воды"),
        new("DHW_TES", "Линия DHW. Температура потока воды"),
        new("DHW_TUS", "Линия DHW. Температура обратной воды")
    ];

    /// <summary>
    /// Поля датчика Pt100 в PLC.
    /// </summary>
    public static class Fields
    {
        public const string Value = "Value";
        public const string ValueAct = "Value_Act";
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
