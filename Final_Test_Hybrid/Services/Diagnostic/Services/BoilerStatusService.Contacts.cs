using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Models.Enums;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения статуса и конфигурации котла - состояние контактов и актуаторов.
/// </summary>
public partial class BoilerStatusService
{
    #region Register Addresses - Contacts

    private const ushort RegisterStbThermostat = 1066;
    private const ushort RegisterPneumaticSwitch = 1067;
    private const ushort RegisterPressureSensor = 1068;
    private const ushort RegisterRoomThermostat = 1071;
    private const ushort RegisterThreeWayValve = 1069;
    private const ushort RegisterPumpState = 1070;

    #endregion

    #region Contacts State (reg 1066-1068, 1071)

    /// <summary>
    /// Читает состояние термостата STB (защита от перегрева).
    /// </summary>
    /// <remarks>
    /// Регистр 1066. Closed = нормальная работа, Open = сработала защита.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием контакта.</returns>
    public async Task<DiagnosticReadResult<ContactState>> ReadSTBThermostatAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterStbThermostat - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<ContactState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<ContactState>.Ok(address, (ContactState)result.Value);
    }

    /// <summary>
    /// Читает состояние пневматического выключателя.
    /// </summary>
    /// <remarks>
    /// Регистр 1067. Контролирует наличие тяги в дымоходе.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием контакта.</returns>
    public async Task<DiagnosticReadResult<ContactState>> ReadPneumaticSwitchAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPneumaticSwitch - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<ContactState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<ContactState>.Ok(address, (ContactState)result.Value);
    }

    /// <summary>
    /// Читает состояние датчика/реле давления.
    /// </summary>
    /// <remarks>
    /// Регистр 1068. Контролирует давление воды в системе.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием контакта.</returns>
    public async Task<DiagnosticReadResult<ContactState>> ReadPressureSensorAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPressureSensor - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<ContactState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<ContactState>.Ok(address, (ContactState)result.Value);
    }

    /// <summary>
    /// Читает состояние комнатного термостата.
    /// </summary>
    /// <remarks>
    /// Регистр 1071. Closed = требуется нагрев, Open = температура достигнута.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием контакта.</returns>
    public async Task<DiagnosticReadResult<ContactState>> ReadRoomThermostatAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterRoomThermostat - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<ContactState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<ContactState>.Ok(address, (ContactState)result.Value);
    }

    #endregion

    #region Actuators State (reg 1069-1070)

    /// <summary>
    /// Читает состояние трёхходового клапана.
    /// </summary>
    /// <remarks>
    /// Регистр 1069. Показывает текущее положение клапана переключения ОС/ГВС.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием клапана.</returns>
    public async Task<DiagnosticReadResult<ThreeWayValveState>> ReadThreeWayValveAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterThreeWayValve - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<ThreeWayValveState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<ThreeWayValveState>.Ok(address, (ThreeWayValveState)result.Value);
    }

    /// <summary>
    /// Читает состояние циркуляционного насоса.
    /// </summary>
    /// <remarks>
    /// Регистр 1070. Показывает текущее состояние насоса (выключен, включен, ошибка).
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с состоянием насоса.</returns>
    public async Task<DiagnosticReadResult<PumpState>> ReadPumpStateAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPumpState - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<PumpState>.Fail(address, result.Error!);
        }

        return DiagnosticReadResult<PumpState>.Ok(address, (PumpState)result.Value);
    }

    #endregion
}
