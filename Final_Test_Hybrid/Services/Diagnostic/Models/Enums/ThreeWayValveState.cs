namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Состояние трёхходового клапана.
/// </summary>
/// <remarks>
/// Регистр 1069. Показывает текущее положение клапана переключения ОС/ГВС.
/// </remarks>
public enum ThreeWayValveState : ushort
{
    /// <summary>Состояние неизвестно.</summary>
    Unknown = 0,

    /// <summary>Переход в положение ОС.</summary>
    TransitionToOS = 1,

    /// <summary>Положение ОС (отопление).</summary>
    OS = 2,

    /// <summary>Переход в положение ГВС.</summary>
    TransitionToDHW = 3,

    /// <summary>Положение ГВС (горячее водоснабжение).</summary>
    DHW = 4,

    /// <summary>Ошибка клапана.</summary>
    Error = 5
}
