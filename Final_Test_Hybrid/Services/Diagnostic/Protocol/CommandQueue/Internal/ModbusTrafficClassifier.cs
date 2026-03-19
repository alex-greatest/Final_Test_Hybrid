namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Определяет класс трафика по источнику Modbus-команды.
/// </summary>
internal static class ModbusTrafficClassifier
{
    private const string UiSourcePrefix = "UI.";

    public static ModbusTrafficClass GetTrafficClass(string? source)
    {
        return source?.StartsWith(UiSourcePrefix, StringComparison.Ordinal) == true
            ? ModbusTrafficClass.NonCritical
            : ModbusTrafficClass.Critical;
    }
}
