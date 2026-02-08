namespace Final_Test_Hybrid.Models.Plc.Tags;

/// <summary>
/// Теги ПИД-регуляторов для компонента PidRegulatorCheck.
/// Адрес: DB_ControlValve
/// </summary>
public static class PidRegulatorTags
{
    private const string BaseAddress = "ns=3;s=\"DB_ControlValve\"";

    /// <summary>
    /// Список всех ПИД-регуляторов.
    /// </summary>
    public static readonly IReadOnlyList<RegulatorInfo> Regulators =
    [
        new("VRP2_1", "Регулятор VRP2_1"),
        new("VPP3_1", "Регулятор VPP3_1"),
        new("VRP2_2", "Регулятор VRP2_2")
    ];

    /// <summary>
    /// Поля ПИД-регулятора в PLC.
    /// </summary>
    public static class Fields
    {
        public const string SetPoint = "SetPoint";
        public const string ActuelValue = "ActuelValue";
        public const string ManualValue = "ManualValue";
        public const string ActuelloutValue = "ActuelloutValue";
        public const string Gain1 = "Gain1";
        public const string Ti1 = "Ti1";
        public const string Td1 = "Td1";
        public const string Gain2 = "Gain2";
        public const string Ti2 = "Ti2";
        public const string Td2 = "Td2";
    }

    /// <summary>
    /// Формирует NodeId для тега регулятора.
    /// </summary>
    /// <param name="regulatorName">Имя регулятора.</param>
    /// <param name="field">Поле регулятора.</param>
    /// <returns>NodeId для OPC-UA.</returns>
    public static string BuildNodeId(string regulatorName, string field) =>
        $"{BaseAddress}.\"{regulatorName}\".\"{field}\"";
}

/// <summary>
/// Информация о ПИД-регуляторе.
/// </summary>
/// <param name="Name">Имя тега в PLC.</param>
/// <param name="Description">Описание регулятора.</param>
public record RegulatorInfo(string Name, string Description);
