using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Models.Enums;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения статуса и конфигурации котла.
/// </summary>
public partial class BoilerStatusService(
    RegisterReader reader,
    IOptions<DiagnosticSettings> settings,
    ILogger<BoilerStatusService> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<BoilerStatusService> _logger = new(logger, testStepLogger);
    #region Register Addresses

    private const ushort RegisterStatus = 1005;
    private const ushort RegisterBoilerPowerType = 1002;
    private const ushort RegisterPumpType = 1003;
    private const ushort RegisterPressureDeviceType = 1004;
    private const ushort RegisterGasRegulatorType = 1157;
    private const ushort RegisterConnectionType = 1054;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Status (reg 1005)

    /// <summary>
    /// Читает текущий статус работы котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1005. Возвращает состояние работы котла (режим ожидания, нагрев ОС/ГВС, блокировка и т.д.).
    /// Значение -1 означает режим тестирования на линии.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с текущим статусом котла.</returns>
    public async Task<DiagnosticReadResult<BoilerStatus>> ReadStatusAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterStatus - _settings.BaseAddressOffset);
        var result = await reader.ReadInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения статуса котла: {Error}", result.Error!);
            return DiagnosticReadResult<BoilerStatus>.Fail(address, result.Error!);
        }

        var status = (BoilerStatus)result.Value;
        _logger.LogDebug("Статус котла: {Status}", status);
        return DiagnosticReadResult<BoilerStatus>.Ok(address, status);
    }

    #endregion

    #region Configuration (reg 1002-1004, 1157)

    /// <summary>
    /// Читает тип мощности котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1002 (скрытый параметр 3.1.A). Определяет номинальную мощность котла.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом мощности.</returns>
    public async Task<DiagnosticReadResult<BoilerPowerType>> ReadBoilerPowerTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBoilerPowerType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа мощности котла: {Error}", result.Error!);
            return DiagnosticReadResult<BoilerPowerType>.Fail(address, result.Error!);
        }

        var powerType = (BoilerPowerType)result.Value;
        _logger.LogDebug("Тип мощности котла: {PowerType}", powerType);
        return DiagnosticReadResult<BoilerPowerType>.Ok(address, powerType);
    }

    /// <summary>
    /// Читает тип насоса котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1003 (скрытый параметр 3.1.b). Определяет модель установленного насоса.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом насоса.</returns>
    public async Task<DiagnosticReadResult<PumpType>> ReadPumpTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPumpType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа насоса: {Error}", result.Error!);
            return DiagnosticReadResult<PumpType>.Fail(address, result.Error!);
        }

        var pumpType = (PumpType)result.Value;
        _logger.LogDebug("Тип насоса: {PumpType}", pumpType);
        return DiagnosticReadResult<PumpType>.Ok(address, pumpType);
    }

    /// <summary>
    /// Читает тип устройства контроля давления.
    /// </summary>
    /// <remarks>
    /// Регистр 1004 (скрытый параметр 3.1.C). Определяет тип датчика/реле давления.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом устройства давления.</returns>
    public async Task<DiagnosticReadResult<PressureDeviceType>> ReadPressureDeviceTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPressureDeviceType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа устройства давления: {Error}", result.Error!);
            return DiagnosticReadResult<PressureDeviceType>.Fail(address, result.Error!);
        }

        var deviceType = (PressureDeviceType)result.Value;
        _logger.LogDebug("Тип устройства давления: {DeviceType}", deviceType);
        return DiagnosticReadResult<PressureDeviceType>.Ok(address, deviceType);
    }

    /// <summary>
    /// Читает тип регулятора давления газа.
    /// </summary>
    /// <remarks>
    /// Регистр 1157 (скрытый параметр 3.1.d). Определяет модель газового клапана.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом регулятора газа.</returns>
    public async Task<DiagnosticReadResult<GasRegulatorType>> ReadGasRegulatorTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterGasRegulatorType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа регулятора газа: {Error}", result.Error!);
            return DiagnosticReadResult<GasRegulatorType>.Fail(address, result.Error!);
        }

        var regulatorType = (GasRegulatorType)result.Value;
        _logger.LogDebug("Тип регулятора газа: {RegulatorType}", regulatorType);
        return DiagnosticReadResult<GasRegulatorType>.Ok(address, regulatorType);
    }

    #endregion

    #region System Type (reg 1054)

    /// <summary>
    /// Читает тип подключения системы отопления.
    /// </summary>
    /// <remarks>
    /// Регистр 1054. Определяет конфигурацию гидравлической схемы.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом подключения.</returns>
    public async Task<DiagnosticReadResult<ConnectionType>> ReadConnectionTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterConnectionType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа подключения: {Error}", result.Error!);
            return DiagnosticReadResult<ConnectionType>.Fail(address, result.Error!);
        }

        var connectionType = (ConnectionType)result.Value;
        _logger.LogDebug("Тип подключения: {ConnectionType}", connectionType);
        return DiagnosticReadResult<ConnectionType>.Ok(address, connectionType);
    }

    #endregion
}
