using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения температур и сопротивлений датчиков котла.
/// </summary>
public class BoilerTemperatureService(
    RegisterReader reader,
    IOptions<DiagnosticSettings> settings)
{
    #region Register Addresses - Temperatures

    private const ushort RegisterSupplyLineTemperature = 1006;
    private const ushort RegisterDhwTemperature = 1009;
    private const ushort RegisterBoilerTemperature = 1011;
    private const ushort RegisterOutdoorTemperature = 1098;

    #endregion

    #region Register Addresses - Resistances

    private const ushort RegisterSupplyLineResistance = 1007;
    private const ushort RegisterDhwResistance = 1010;
    private const ushort RegisterBoilerResistance = 1012;
    private const ushort RegisterOutdoorResistance = 1099;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Temperatures (reg 1006, 1009, 1011, 1098)

    /// <summary>
    /// Читает температуру подающей линии (подача).
    /// </summary>
    /// <remarks>
    /// Регистр 1006. Единица измерения: C.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с температурой в градусах Цельсия.</returns>
    public async Task<DiagnosticReadResult<short>> ReadSupplyLineTemperatureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterSupplyLineTemperature - _settings.BaseAddressOffset);
        return await reader.ReadInt16Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает температуру горячего водоснабжения (ГВС).
    /// </summary>
    /// <remarks>
    /// Регистр 1009. Единица измерения: C.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с температурой в градусах Цельсия.</returns>
    public async Task<DiagnosticReadResult<short>> ReadDHWTemperatureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterDhwTemperature - _settings.BaseAddressOffset);
        return await reader.ReadInt16Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает температуру бойлера косвенного нагрева.
    /// </summary>
    /// <remarks>
    /// Регистр 1011. Единица измерения: C.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с температурой в градусах Цельсия.</returns>
    public async Task<DiagnosticReadResult<short>> ReadBoilerTemperatureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBoilerTemperature - _settings.BaseAddressOffset);
        return await reader.ReadInt16Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает наружную температуру.
    /// </summary>
    /// <remarks>
    /// Регистр 1098. Единица измерения: C. Требуется внешний датчик.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с температурой в градусах Цельсия.</returns>
    public async Task<DiagnosticReadResult<short>> ReadOutdoorTemperatureAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterOutdoorTemperature - _settings.BaseAddressOffset);
        return await reader.ReadInt16Async(address, ct).ConfigureAwait(false);
    }

    #endregion

    #region Sensor Resistances (reg 1007, 1010, 1012, 1099)

    /// <summary>
    /// Читает сопротивление датчика подающей линии.
    /// </summary>
    /// <remarks>
    /// Регистр 1007. Единица измерения: Ом. Используется для диагностики датчика.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с сопротивлением в омах.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadSupplyLineResistanceAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterSupplyLineResistance - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает сопротивление датчика ГВС.
    /// </summary>
    /// <remarks>
    /// Регистр 1010. Единица измерения: Ом. Используется для диагностики датчика.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с сопротивлением в омах.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadDHWResistanceAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterDhwResistance - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает сопротивление датчика бойлера.
    /// </summary>
    /// <remarks>
    /// Регистр 1012. Единица измерения: Ом. Используется для диагностики датчика.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с сопротивлением в омах.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadBoilerResistanceAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBoilerResistance - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает сопротивление датчика наружной температуры.
    /// </summary>
    /// <remarks>
    /// Регистр 1099. Единица измерения: Ом. Используется для диагностики датчика.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с сопротивлением в омах.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadOutdoorResistanceAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterOutdoorResistance - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    #endregion
}
