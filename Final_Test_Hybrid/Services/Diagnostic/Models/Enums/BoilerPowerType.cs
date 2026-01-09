namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Тип мощности котла.
/// </summary>
/// <remarks>
/// Регистр 1002 (скрытый параметр 3.1.A). Определяет номинальную мощность котла.
/// </remarks>
public enum BoilerPowerType : ushort
{
    /// <summary>Тип не задан.</summary>
    NotSet = 0,

    /// <summary>Тип 21 - 18 кВт.</summary>
    Type21_18kW = 1,

    /// <summary>Тип 31 - 24 кВт.</summary>
    Type31_24kW = 2,

    /// <summary>Тип 41 - 28 кВт.</summary>
    Type41_28kW = 3
}
