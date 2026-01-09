namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Текущий статус работы котла.
/// </summary>
/// <remarks>
/// Регистр 1005. Значение -1 означает режим тестирования на линии.
/// </remarks>
public enum BoilerStatus : short
{
    /// <summary>Режим тестирования на линии.</summary>
    TestingMode = -1,

    /// <summary>Включение питания.</summary>
    PowerOn = 0,

    /// <summary>Блокировка Б (сбрасываемая).</summary>
    BlockB = 1,

    /// <summary>Блокировка А (несбрасываемая).</summary>
    BlockA = 2,

    /// <summary>Аварийный режим.</summary>
    EmergencyMode = 3,

    /// <summary>Режим ожидания.</summary>
    StandbyMode = 4,

    /// <summary>Режим установки мощности.</summary>
    SetPowerMode = 5,

    /// <summary>Режим короткого нагрева ОС.</summary>
    ShortHeatingOS = 6,

    /// <summary>Режим постоянного нагрева ОС.</summary>
    ConstantHeatingOS = 7,

    /// <summary>Режим длинной продувки.</summary>
    LongPurge = 8,

    /// <summary>Режим "Лето" (нагрев ГВС).</summary>
    SummerMode = 9,

    /// <summary>Режим "Зима" (нагрев ОС + ГВС).</summary>
    WinterMode = 10
}
