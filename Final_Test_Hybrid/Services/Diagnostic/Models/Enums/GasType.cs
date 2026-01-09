namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Вид используемого газа.
/// </summary>
/// <remarks>
/// Регистр 1065. Влияет на параметры горения.
/// </remarks>
public enum GasType : ushort
{
    /// <summary>Природный газ (метан).</summary>
    NaturalGas = 0,

    /// <summary>Сжиженный газ (пропан-бутан).</summary>
    LPG = 1
}
