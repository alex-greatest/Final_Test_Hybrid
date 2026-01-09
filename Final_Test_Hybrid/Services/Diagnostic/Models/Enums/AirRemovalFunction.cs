namespace Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

/// <summary>
/// Функция удаления воздуха из системы.
/// </summary>
/// <remarks>
/// Регистр 1033. Включает/выключает автоматическое удаление воздуха при запуске.
/// </remarks>
public enum AirRemovalFunction : ushort
{
    /// <summary>Функция отключена.</summary>
    Disabled = 0,

    /// <summary>Функция включена.</summary>
    Enabled = 1
}
