namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Режим работы насоса (управление ШИМ).
/// </summary>
/// <remarks>
/// Регистр 1108. Определяет способ управления скоростью насоса.
/// </remarks>
public enum PumpWorkMode : ushort
{
    /// <summary>Без ШИМ (постоянная скорость).</summary>
    NoPWM = 0,

    /// <summary>Ручной ШИМ.</summary>
    ManualPWM = 1,

    /// <summary>Автоматический ШИМ.</summary>
    AutoPWM = 2
}
