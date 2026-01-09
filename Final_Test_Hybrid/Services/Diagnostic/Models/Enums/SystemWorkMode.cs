namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Режим работы системы (мощность/температура).
/// </summary>
/// <remarks>
/// Используется для регистров 1036 (режим мощности ОС) и 1058 (режим ГВС).
/// Для регистра 1036 доступно дополнительное значение MaxMode (4).
/// </remarks>
public enum SystemWorkMode : ushort
{
    /// <summary>Нормальный режим.</summary>
    Normal = 0,

    /// <summary>Минимальный режим.</summary>
    Min = 1,

    /// <summary>Максимальный режим.</summary>
    Max = 2,

    /// <summary>Регулируемый режим.</summary>
    Adjustable = 3,

    /// <summary>Режим максимальной мощности (только для регистра 1036).</summary>
    MaxMode = 4
}
