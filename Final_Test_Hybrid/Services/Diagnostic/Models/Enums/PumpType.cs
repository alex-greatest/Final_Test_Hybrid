namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Тип насоса котла.
/// </summary>
/// <remarks>
/// Регистр 1003 (скрытый параметр 3.1.b). Определяет модель установленного насоса.
/// </remarks>
public enum PumpType : ushort
{
    /// <summary>Тип не задан.</summary>
    NotSet = 0,

    /// <summary>Насос DWP15-50-G1.</summary>
    DWP15_50_G1 = 1
}
