namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Режим работы вентилятора.
/// </summary>
/// <remarks>
/// Регистр 1034. Определяет способ управления скоростью вентилятора.
/// </remarks>
public enum FanWorkMode : ushort
{
    /// <summary>Модуляция (плавное регулирование).</summary>
    Modulation = 0,

    /// <summary>Максимальная скорость ступени.</summary>
    MaxSpeedOfStep = 1
}
