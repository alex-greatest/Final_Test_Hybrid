namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Режим тестирования на линии.
/// </summary>
/// <remarks>
/// Регистр 1189. Используется для тестирования гидравлики на производственной линии.
/// </remarks>
public enum TestingLineMode : ushort
{
    /// <summary>Режим тестирования выключен.</summary>
    Off = 0,

    /// <summary>Поток на ГВС, насос включен.</summary>
    FlowToDHW_PumpOn = 1,

    /// <summary>Поток на ОС, насос включен.</summary>
    FlowToOS_PumpOn = 2
}
