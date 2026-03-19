namespace Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;

/// <summary>
/// Внутренняя классификация Modbus-трафика для reconnect-политики.
/// </summary>
internal enum ModbusTrafficClass
{
    Critical = 0,
    NonCritical = 1
}
