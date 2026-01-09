namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Состояние циркуляционного насоса.
/// </summary>
/// <remarks>
/// Регистр 1070. Показывает текущее состояние насоса.
/// </remarks>
public enum PumpState : ushort
{
    /// <summary>Насос выключен.</summary>
    Off = 0,

    /// <summary>Насос включен.</summary>
    On = 1,

    /// <summary>Ошибка насоса.</summary>
    Error = 2
}
