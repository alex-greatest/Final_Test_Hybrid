namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Тип устройства контроля давления.
/// </summary>
/// <remarks>
/// Регистр 1004 (скрытый параметр 3.1.C). Определяет тип датчика/реле давления.
/// </remarks>
public enum PressureDeviceType : ushort
{
    /// <summary>Тип не задан.</summary>
    NotSet = 0,

    /// <summary>Реле давления.</summary>
    PressureRelay = 1
}
