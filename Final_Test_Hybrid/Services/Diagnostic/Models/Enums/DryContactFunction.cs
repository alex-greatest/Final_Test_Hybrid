namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Функция сухого контакта.
/// </summary>
/// <remarks>
/// Регистр 1100. Определяет назначение входа сухого контакта.
/// </remarks>
public enum DryContactFunction : ushort
{
    /// <summary>Функция отключена.</summary>
    Disabled = 0,

    /// <summary>Динамическая блокировка.</summary>
    DynamicBlock = 1,

    /// <summary>Одиночная блокировка.</summary>
    SingleBlock = 2,

    /// <summary>Включение нагрева ОС.</summary>
    OnOSHeating = 3,

    /// <summary>Установка ошибки.</summary>
    OnErrorSet = 4
}
