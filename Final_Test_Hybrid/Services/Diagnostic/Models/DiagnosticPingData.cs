namespace Final_Test_Hybrid.Services.Diagnostic.Models;

/// <summary>
/// Данные ping-опроса. Расширяемая структура для будущих параметров.
/// </summary>
public record DiagnosticPingData
{
    /// <summary>
    /// Ключ режима: стенд (0xD7F8DB56), инженерный (0xFA87CD5E), обычный (иное).
    /// </summary>
    public uint ModeKey { get; init; }

    /// <summary>
    /// Статус котла: -1 тест, 0 включение, 1-10 различные режимы.
    /// </summary>
    public short BoilerStatus { get; init; }

    /// <summary>
    /// ID последней ошибки ЭБУ.
    /// - null = не удалось прочитать регистр ошибки (soft-fail)
    /// - 0 = ошибок нет
    /// - 1-25 = код ошибки
    /// </summary>
    public ushort? LastErrorId { get; init; }

    /// <summary>
    /// Температура подающей линии CH (регистр 1006).
    /// null = не удалось прочитать (soft-fail).
    /// </summary>
    public short? ChTemperature { get; init; }
}
