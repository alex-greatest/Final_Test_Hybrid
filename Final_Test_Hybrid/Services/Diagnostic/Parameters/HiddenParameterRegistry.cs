namespace Final_Test_Hybrid.Services.Diagnostic.Parameters;

/// <summary>
/// Реестр скрытых параметров котла (п.4.6 протокола).
/// </summary>
public static class HiddenParameterRegistry
{
    /// <summary>
    /// Мощность котла (3.1.A).
    /// </summary>
    public static readonly HiddenParameter BoilerPower = new(
        "3.1.A",
        "Мощность котла",
        ReadAddress: 1002,
        Hidden1Address: 1147,
        Hidden2Address: 1148);

    /// <summary>
    /// Тип насоса (3.P.P).
    /// </summary>
    public static readonly HiddenParameter PumpType = new(
        "3.P.P",
        "Тип насоса",
        ReadAddress: 1003,
        Hidden1Address: 1149,
        Hidden2Address: 1150);

    /// <summary>
    /// Датчик давления (3.P.E).
    /// </summary>
    public static readonly HiddenParameter PressureDevice = new(
        "3.P.E",
        "Датчик давления",
        ReadAddress: 1004,
        Hidden1Address: 1151,
        Hidden2Address: 1152);

    /// <summary>
    /// Регулятор газа (3.P.C).
    /// </summary>
    public static readonly HiddenParameter GasRegulator = new(
        "3.P.C",
        "Регулятор газа",
        ReadAddress: 1157,
        Hidden1Address: 1158,
        Hidden2Address: 1159);

    /// <summary>
    /// Все скрытые параметры.
    /// </summary>
    public static IReadOnlyList<HiddenParameter> All =>
        [BoilerPower, PumpType, PressureDevice, GasRegulator];

    /// <summary>
    /// Поиск параметра по имени.
    /// </summary>
    public static HiddenParameter? FindByName(string name) =>
        All.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// Поиск параметра по адресу чтения.
    /// </summary>
    public static HiddenParameter? FindByReadAddress(ushort address) =>
        All.FirstOrDefault(p => p.ReadAddress == address);
}
