namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

/// <summary>
/// Ambient-контекст для диагностического источника Modbus-команд.
/// </summary>
public static class ModbusCommandTraceContext
{
    private const string DefaultSource = "Unknown";
    private static readonly AsyncLocal<string?> CurrentSourceHolder = new();

    /// <summary>
    /// Возвращает текущий источник либо значение по умолчанию.
    /// </summary>
    public static string CurrentSource
    {
        get
        {
            var source = CurrentSourceHolder.Value;
            return string.IsNullOrWhiteSpace(source) ? DefaultSource : source;
        }
    }

    /// <summary>
    /// Устанавливает источник для текущего async-flow.
    /// </summary>
    public static IDisposable BeginScope(string source)
    {
        var previousSource = CurrentSourceHolder.Value;
        CurrentSourceHolder.Value = string.IsNullOrWhiteSpace(source) ? previousSource : source;
        return new RestoreScope(previousSource);
    }

    private sealed class RestoreScope(string? previousSource) : IDisposable
    {
        public void Dispose()
        {
            CurrentSourceHolder.Value = previousSource;
        }
    }
}
