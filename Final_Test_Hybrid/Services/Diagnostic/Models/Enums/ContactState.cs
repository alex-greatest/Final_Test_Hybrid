namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Состояние контакта (замкнут/разомкнут).
/// </summary>
/// <remarks>
/// Используется для регистров 1066-1068, 1071, 1101.
/// Применяется к термостату STB, пневматическому выключателю, датчику давления,
/// комнатному термостату и сухому контакту.
/// </remarks>
public enum ContactState : ushort
{
    /// <summary>Контакт разомкнут.</summary>
    Open = 0,

    /// <summary>Контакт замкнут.</summary>
    Closed = 1
}
