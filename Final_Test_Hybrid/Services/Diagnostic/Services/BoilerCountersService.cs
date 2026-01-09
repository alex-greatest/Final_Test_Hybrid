using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения счётчиков котла.
/// </summary>
public class BoilerCountersService(
    RegisterReader reader,
    IOptions<DiagnosticSettings> settings)
{
    #region Register Addresses

    private const ushort RegisterBurnerStartsOs = 1072;
    private const ushort RegisterBurnerStartsDhw = 1074;
    private const ushort RegisterBurnerWorkTimeOs = 1076;
    private const ushort RegisterBurnerWorkTimeDhw = 1078;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Burner Starts (reg 1072-1075)

    /// <summary>
    /// Читает количество запусков горелки для отопления (ОС).
    /// </summary>
    /// <remarks>
    /// Регистры 1072-1073 (uint32). Накопительный счётчик запусков в режиме отопления.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с количеством запусков.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadBurnerStartsOSAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBurnerStartsOs - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Читает количество запусков горелки для ГВС.
    /// </summary>
    /// <remarks>
    /// Регистры 1074-1075 (uint32). Накопительный счётчик запусков в режиме ГВС.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с количеством запусков.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadBurnerStartsDHWAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBurnerStartsDhw - _settings.BaseAddressOffset);
        return await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);
    }

    #endregion

    #region Burner Work Time (reg 1076-1079)

    /// <summary>
    /// Читает время работы горелки для отопления (ОС).
    /// </summary>
    /// <remarks>
    /// Регистры 1076-1077 (uint32). Накопительное время работы в секундах, конвертируется в TimeSpan.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с временем работы.</returns>
    public async Task<DiagnosticReadResult<TimeSpan>> ReadBurnerWorkTimeOSAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBurnerWorkTimeOs - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<TimeSpan>.Fail(address, result.Error!);
        }

        var timeSpan = TimeSpan.FromSeconds(result.Value);
        return DiagnosticReadResult<TimeSpan>.Ok(address, timeSpan);
    }

    /// <summary>
    /// Читает время работы горелки для ГВС.
    /// </summary>
    /// <remarks>
    /// Регистры 1078-1079 (uint32). Накопительное время работы в секундах, конвертируется в TimeSpan.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с временем работы.</returns>
    public async Task<DiagnosticReadResult<TimeSpan>> ReadBurnerWorkTimeDHWAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBurnerWorkTimeDhw - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            return DiagnosticReadResult<TimeSpan>.Fail(address, result.Error!);
        }

        var timeSpan = TimeSpan.FromSeconds(result.Value);
        return DiagnosticReadResult<TimeSpan>.Ok(address, timeSpan);
    }

    #endregion
}
