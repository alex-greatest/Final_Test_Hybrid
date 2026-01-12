using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Models.Enums;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения/записи настроек котла.
/// </summary>
/// <remarks>
/// Методы записи требуют уровень доступа Engineering или Stand.
/// </remarks>
public class BoilerSettingsService(
    RegisterReader reader,
    RegisterWriter writer,
    IOptions<DiagnosticSettings> settings,
    ILogger<BoilerSettingsService> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<BoilerSettingsService> _logger = new(logger, testStepLogger);
    #region Register Addresses

    private const ushort RegisterGasType = 1065;
    private const ushort RegisterDhwSetpoint = 1013;
    private const ushort RegisterMaxDhwTemperature = 1043;
    private const ushort RegisterTestingLineMode = 1189;
    private const ushort RegisterFactoryReset = 1057;
    private const ushort RegisterClearBlockage = 1153;
    private const ushort RegisterClearErrorLog = 1154;

    #endregion

    #region Command Values

    /// <summary>
    /// Значение команды для запуска операции (сброс, очистка).
    /// </summary>
    private const ushort CommandExecute = 1;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Gas Type (reg 1065)

    /// <summary>
    /// Читает вид используемого газа.
    /// </summary>
    /// <remarks>
    /// Регистр 1065. Влияет на параметры горения.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с типом газа.</returns>
    public async Task<DiagnosticReadResult<GasType>> ReadGasTypeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterGasType - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения типа газа: {Error}", result.Error!);
            return DiagnosticReadResult<GasType>.Fail(address, result.Error!);
        }

        var gasType = (GasType)result.Value;
        _logger.LogDebug("Тип газа: {GasType}", gasType);
        return DiagnosticReadResult<GasType>.Ok(address, gasType);
    }

    /// <summary>
    /// Записывает вид используемого газа.
    /// </summary>
    /// <remarks>
    /// Регистр 1065. Требует уровень доступа Engineering или Stand.
    /// </remarks>
    /// <param name="value">Тип газа (природный или сжиженный).</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> WriteGasTypeAsync(GasType value, CancellationToken ct = default)
    {
        var address = (ushort)(RegisterGasType - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, (ushort)value, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Записан тип газа: {GasType}", value);
        }
        else
        {
            _logger.LogError("Ошибка записи типа газа: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region DHW Setpoint (reg 1013)

    /// <summary>
    /// Читает уставку температуры ГВС.
    /// </summary>
    /// <remarks>
    /// Регистр 1013. Единица измерения: C. Целевая температура горячей воды.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с уставкой температуры.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadDHWSetpointAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterDhwSetpoint - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Уставка ГВС: {Value} C", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения уставки ГВС: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Записывает уставку температуры ГВС.
    /// </summary>
    /// <remarks>
    /// Регистр 1013. Единица измерения: C. Требует уровень доступа Engineering или Stand.
    /// Типичный диапазон: 35-60C.
    /// </remarks>
    /// <param name="value">Уставка температуры в градусах Цельсия.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> WriteDHWSetpointAsync(ushort value, CancellationToken ct = default)
    {
        var address = (ushort)(RegisterDhwSetpoint - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, value, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Записана уставка ГВС: {Value} C", value);
        }
        else
        {
            _logger.LogError("Ошибка записи уставки ГВС: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Max DHW Temperature (reg 1043)

    /// <summary>
    /// Читает максимальную температуру ГВС.
    /// </summary>
    /// <remarks>
    /// Регистр 1043. Единица измерения: C. Верхний предел уставки ГВС.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с максимальной температурой.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadMaxDHWTemperatureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterMaxDhwTemperature - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Максимальная температура ГВС: {Value} C", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения макс. температуры ГВС: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Записывает максимальную температуру ГВС.
    /// </summary>
    /// <remarks>
    /// Регистр 1043. Единица измерения: C. Требует уровень доступа Engineering или Stand.
    /// </remarks>
    /// <param name="value">Максимальная температура в градусах Цельсия.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> WriteMaxDHWTemperatureAsync(ushort value, CancellationToken ct = default)
    {
        var address = (ushort)(RegisterMaxDhwTemperature - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, value, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Записана макс. температура ГВС: {Value} C", value);
        }
        else
        {
            _logger.LogError("Ошибка записи макс. температуры ГВС: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Testing Line Mode (reg 1189)

    /// <summary>
    /// Читает режим тестирования на линии.
    /// </summary>
    /// <remarks>
    /// Регистр 1189. Используется для тестирования гидравлики на производственной линии.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с режимом тестирования.</returns>
    public async Task<DiagnosticReadResult<TestingLineMode>> ReadTestingLineModeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterTestingLineMode - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения режима тестирования на линии: {Error}", result.Error!);
            return DiagnosticReadResult<TestingLineMode>.Fail(address, result.Error!);
        }

        var mode = (TestingLineMode)result.Value;
        _logger.LogDebug("Режим тестирования на линии: {Mode}", mode);
        return DiagnosticReadResult<TestingLineMode>.Ok(address, mode);
    }

    /// <summary>
    /// Записывает режим тестирования на линии.
    /// </summary>
    /// <remarks>
    /// Регистр 1189. Требует уровень доступа Engineering или Stand.
    /// Используется для проверки трёхходового клапана и насоса.
    /// </remarks>
    /// <param name="mode">Режим тестирования.</param>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> WriteTestingLineModeAsync(TestingLineMode mode, CancellationToken ct = default)
    {
        var address = (ushort)(RegisterTestingLineMode - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, (ushort)mode, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Записан режим тестирования на линии: {Mode}", mode);
        }
        else
        {
            _logger.LogError("Ошибка записи режима тестирования на линии: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Reset Operations (reg 1057, 1153, 1154)

    /// <summary>
    /// Сбрасывает настройки котла до заводских.
    /// </summary>
    /// <remarks>
    /// Регистр 1057. Запись значения 1 инициирует сброс. Требует уровень доступа Engineering или Stand.
    /// После сброса котёл перезагружается.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> ResetToFactoryDefaultsAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFactoryReset - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, CommandExecute, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Выполнен сброс к заводским настройкам");
        }
        else
        {
            _logger.LogError("Ошибка сброса к заводским настройкам: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Сбрасывает блокировку котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1153. Запись значения 1 сбрасывает текущую блокировку. Требует уровень доступа Engineering или Stand.
    /// Работает только для сбрасываемых блокировок (тип Б).
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> ClearBlockageAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterClearBlockage - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, CommandExecute, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Выполнен сброс блокировки котла");
        }
        else
        {
            _logger.LogError("Ошибка сброса блокировки котла: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Очищает журнал ошибок котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1154. Запись значения 1 очищает журнал. Требует уровень доступа Engineering или Stand.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат записи.</returns>
    public async Task<DiagnosticWriteResult> ClearErrorLogAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterClearErrorLog - _settings.BaseAddressOffset);
        var result = await writer.WriteUInt16Async(address, CommandExecute, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation("Выполнена очистка журнала ошибок");
        }
        else
        {
            _logger.LogError("Ошибка очистки журнала ошибок: {Error}", result.Error!);
        }

        return result;
    }

    #endregion
}
