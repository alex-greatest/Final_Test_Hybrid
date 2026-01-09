namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Тип регулятора давления газа.
/// </summary>
/// <remarks>
/// Регистр 1157 (скрытый параметр 3.1.d). Определяет модель газового клапана.
/// </remarks>
public enum GasRegulatorType : ushort
{
    /// <summary>Тип не задан.</summary>
    NotSet = 0,

    /// <summary>Регулятор EBR2008N010901.</summary>
    EBR2008N010901 = 1
}
