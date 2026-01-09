namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Тип подключения системы отопления.
/// </summary>
/// <remarks>
/// Регистр 1054. Определяет конфигурацию гидравлической схемы.
/// </remarks>
public enum ConnectionType : ushort
{
    /// <summary>Одноконтурная система.</summary>
    SingleCircuit = 0,

    /// <summary>Двухконтурная система.</summary>
    DualCircuit = 1,

    /// <summary>Бойлер с датчиком NTC.</summary>
    BoilerWithNTC = 2,

    /// <summary>Бойлер с термостатом.</summary>
    BoilerWithThermostat = 3
}
