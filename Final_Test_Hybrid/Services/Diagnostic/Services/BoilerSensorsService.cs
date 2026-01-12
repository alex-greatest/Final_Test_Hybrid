using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения датчиков и актуаторов котла.
/// </summary>
public class BoilerSensorsService(
    RegisterReader reader,
    IOptions<DiagnosticSettings> settings,
    ILogger<BoilerSensorsService> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<BoilerSensorsService> _logger = new(logger, testStepLogger);
    #region Register Addresses

    private const ushort RegisterFlameIonization = 1014;
    private const ushort RegisterFlowRate = 1015;
    private const ushort RegisterModulatingCoilCurrent = 1016;
    private const ushort RegisterFanFrequency = 1017;
    private const ushort RegisterFanSpeed = 1018;
    private const ushort RegisterFanSpeedSetpoint = 1019;
    private const ushort RegisterFanFillFactor = 1020;
    private const ushort RegisterEv1Current = 1023;
    private const ushort RegisterEv2Current = 1028;
    private const ushort RegisterPressure = 1115;

    #endregion

    #region Conversion Constants

    /// <summary>
    /// Делитель для преобразования значения регистра в микроамперы.
    /// </summary>
    private const float IonizationDivisor = 1000.0f;

    /// <summary>
    /// Делитель для преобразования значения регистра в л/мин или Гц.
    /// </summary>
    private const float FlowAndFrequencyDivisor = 256.0f;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Flame Control (reg 1014)

    /// <summary>
    /// Читает ток ионизации пламени.
    /// </summary>
    /// <remarks>
    /// Регистр 1014. Единица измерения: мкА (микроампер).
    /// Значение в регистре делится на 1000 для получения мкА.
    /// Нормальное значение при горении: 1-10 мкА.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с током ионизации в мкА.</returns>
    public async Task<DiagnosticReadResult<float>> ReadFlameIonizationAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFlameIonization - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения тока ионизации: {Error}", result.Error!);
            return DiagnosticReadResult<float>.Fail(address, result.Error!);
        }

        var valueInMicroAmps = result.Value / IonizationDivisor;
        _logger.LogDebug("Ток ионизации: {Value} мкА", valueInMicroAmps);
        return DiagnosticReadResult<float>.Ok(address, valueInMicroAmps);
    }

    #endregion

    #region Flow Rate (reg 1015)

    /// <summary>
    /// Читает расход воды.
    /// </summary>
    /// <remarks>
    /// Регистр 1015. Единица измерения: л/мин.
    /// Значение в регистре делится на 256 для получения л/мин.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с расходом воды в л/мин.</returns>
    public async Task<DiagnosticReadResult<float>> ReadFlowRateAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFlowRate - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения расхода воды: {Error}", result.Error!);
            return DiagnosticReadResult<float>.Fail(address, result.Error!);
        }

        var valueInLitersPerMin = result.Value / FlowAndFrequencyDivisor;
        _logger.LogDebug("Расход воды: {Value} л/мин", valueInLitersPerMin);
        return DiagnosticReadResult<float>.Ok(address, valueInLitersPerMin);
    }

    #endregion

    #region Modulating Coil (reg 1016)

    /// <summary>
    /// Читает ток модулирующей катушки газового клапана.
    /// </summary>
    /// <remarks>
    /// Регистр 1016. Единица измерения: мА (миллиампер).
    /// Управляет степенью открытия газового клапана.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с током в мА.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadModulatingCoilCurrentAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterModulatingCoilCurrent - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Ток модулирующей катушки: {Value} мА", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения тока модулирующей катушки: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Fan (reg 1017-1020)

    /// <summary>
    /// Читает частоту управления вентилятором.
    /// </summary>
    /// <remarks>
    /// Регистр 1017. Единица измерения: Гц.
    /// Значение в регистре делится на 256 для получения Гц.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с частотой в Гц.</returns>
    public async Task<DiagnosticReadResult<float>> ReadFanFrequencyAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFanFrequency - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения частоты вентилятора: {Error}", result.Error!);
            return DiagnosticReadResult<float>.Fail(address, result.Error!);
        }

        var valueInHz = result.Value / FlowAndFrequencyDivisor;
        _logger.LogDebug("Частота вентилятора: {Value} Гц", valueInHz);
        return DiagnosticReadResult<float>.Ok(address, valueInHz);
    }

    /// <summary>
    /// Читает текущую скорость вращения вентилятора.
    /// </summary>
    /// <remarks>
    /// Регистр 1018. Единица измерения: об/мин.
    /// Фактическая скорость по тахосигналу.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения со скоростью в об/мин.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadFanSpeedAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFanSpeed - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Скорость вентилятора: {Value} об/мин", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения скорости вентилятора: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает уставку скорости вращения вентилятора.
    /// </summary>
    /// <remarks>
    /// Регистр 1019. Единица измерения: об/мин.
    /// Целевая скорость, к которой стремится регулятор.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с уставкой скорости в об/мин.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadFanSpeedSetpointAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFanSpeedSetpoint - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Уставка скорости вентилятора: {Value} об/мин", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения уставки скорости вентилятора: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает коэффициент заполнения ШИМ вентилятора.
    /// </summary>
    /// <remarks>
    /// Регистр 1020. Единица измерения: %.
    /// Процент заполнения управляющего сигнала.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с коэффициентом заполнения в %.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadFanFillFactorAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterFanFillFactor - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Коэффициент заполнения ШИМ вентилятора: {Value}%", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения коэффициента заполнения вентилятора: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Gas Valves (reg 1023, 1028)

    /// <summary>
    /// Читает ток защитного клапана EV1.
    /// </summary>
    /// <remarks>
    /// Регистр 1023. Единица измерения: мА (миллиампер).
    /// Входной защитный клапан газа.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с током в мА.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadEV1CurrentAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterEv1Current - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Ток клапана EV1: {Value} мА", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения тока клапана EV1: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает ток защитного клапана EV2.
    /// </summary>
    /// <remarks>
    /// Регистр 1028. Единица измерения: мА (миллиампер).
    /// Выходной защитный клапан газа.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с током в мА.</returns>
    public async Task<DiagnosticReadResult<ushort>> ReadEV2CurrentAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterEv2Current - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Ток клапана EV2: {Value} мА", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения тока клапана EV2: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Pressure (reg 1115-1116)

    /// <summary>
    /// Читает давление воды в системе.
    /// </summary>
    /// <remarks>
    /// Регистры 1115-1116 (float). Единица измерения: бар.
    /// Доступно только для котлов с датчиком давления (не с реле).
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с давлением в барах.</returns>
    public async Task<DiagnosticReadResult<float>> ReadPressureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterPressure - _settings.BaseAddressOffset);
        var result = await reader.ReadFloatAsync(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Давление воды: {Value} бар", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения давления воды: {Error}", result.Error!);
        }

        return result;
    }

    #endregion
}
