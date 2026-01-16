using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Plc;

/// <summary>
/// Хелпер для формирования OPC UA тегов из путей PLC-блоков.
/// </summary>
public static class PlcBlockTagHelper
{
    /// <summary>
    /// Формирует тег Selected для блока.
    /// </summary>
    public static string? GetSelectedTag(IHasPlcBlockPath? step)
    {
        return step == null ? null : $"ns=3;s={QuotePath(step.PlcBlockPath)}.\"Selected\"";
    }

    /// <summary>
    /// Формирует тег Error для блока.
    /// </summary>
    public static string? GetErrorTag(IHasPlcBlockPath? step)
    {
        return step == null ? null : $"ns=3;s={QuotePath(step.PlcBlockPath)}.\"Error\"";
    }

    /// <summary>
    /// Формирует тег End для блока.
    /// </summary>
    public static string? GetEndTag(IHasPlcBlockPath? step)
    {
        return step == null ? null : $"ns=3;s={QuotePath(step.PlcBlockPath)}.\"End\"";
    }

    /// <summary>
    /// Формирует тег Start для блока.
    /// </summary>
    public static string? GetStartTag(IHasPlcBlockPath? step)
    {
        return step == null ? null : $"ns=3;s={QuotePath(step.PlcBlockPath)}.\"Start\"";
    }

    /// <summary>
    /// Кавычит каждый сегмент пути.
    /// "DB_VI.Block_Adapter" -> "\"DB_VI\".\"Block_Adapter\""
    /// </summary>
    private static string QuotePath(string path)
    {
        return string.Join(".", path.Split('.').Select(s => $"\"{s}\""));
    }
}
