using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Сервис чтения информации об устройстве и ошибках котла.
/// </summary>
public class BoilerDeviceInfoService(
    RegisterReader reader,
    IOptions<DiagnosticSettings> settings,
    ILogger<BoilerDeviceInfoService> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<BoilerDeviceInfoService> _logger = new(logger, testStepLogger);
    #region Register Addresses

    private const ushort RegisterFirmwareMajor = 1055;
    private const ushort RegisterFirmwareMinor = 1056;
    private const ushort RegisterSupplierCode = 1131;
    private const ushort RegisterManufactureDate = 1133;
    private const ushort RegisterSerialNumber = 1137;
    private const ushort RegisterArticleNumber = 1139;
    private const ushort RegisterBoilerArticle = 1175;
    private const ushort RegisterItelmaArticle = 1182;
    private const ushort RegisterLastError = 1047;
    private const ushort RegisterErrorLogStart = 1126;

    #endregion

    #region String Length Constants

    private const int ManufactureDateLength = 8;
    private const int ArticleNumberLength = 14;
    private const int ErrorLogRegisterCount = 5;
    private const int MaxErrorsInLog = ErrorLogRegisterCount * 2;
    private const int BitsPerByte = 8;
    private const int LowByteMask = 0xFF;

    #endregion

    private readonly DiagnosticSettings _settings = settings.Value;

    #region Firmware Version (reg 1055-1056)

    /// <summary>
    /// Читает версию прошивки ЭБУ.
    /// </summary>
    /// <remarks>
    /// Регистры 1055-1056. Возвращает кортеж (Major, Minor).
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с версией прошивки (Major, Minor).</returns>
    public async Task<DiagnosticReadResult<(ushort Major, ushort Minor)>> ReadFirmwareVersionAsync(CancellationToken ct = default)
    {
        var addressMajor = (ushort)(RegisterFirmwareMajor - _settings.BaseAddressOffset);
        var majorResult = await ReadFirmwareMajorAsync(addressMajor, ct).ConfigureAwait(false);

        if (!majorResult.Success)
        {
            _logger.LogError("Ошибка чтения версии прошивки (Major): {Error}", majorResult.Error!);
            return DiagnosticReadResult<(ushort, ushort)>.Fail(addressMajor, majorResult.Error!);
        }

        var addressMinor = (ushort)(RegisterFirmwareMinor - _settings.BaseAddressOffset);
        var minorResult = await ReadFirmwareMinorAsync(addressMinor, ct).ConfigureAwait(false);

        if (!minorResult.Success)
        {
            _logger.LogError("Ошибка чтения версии прошивки (Minor): {Error}", minorResult.Error!);
            return DiagnosticReadResult<(ushort, ushort)>.Fail(addressMinor, minorResult.Error!);
        }

        _logger.LogDebug("Версия прошивки: {Major}.{Minor}", majorResult.Value, minorResult.Value);
        return DiagnosticReadResult<(ushort, ushort)>.Ok(addressMajor, (majorResult.Value, minorResult.Value));
    }

    private async Task<DiagnosticReadResult<ushort>> ReadFirmwareMajorAsync(ushort address, CancellationToken ct)
    {
        return await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);
    }

    private async Task<DiagnosticReadResult<ushort>> ReadFirmwareMinorAsync(ushort address, CancellationToken ct)
    {
        return await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);
    }

    #endregion

    #region Device Identification (reg 1131-1145, 1175-1188)

    /// <summary>
    /// Читает код поставщика.
    /// </summary>
    /// <remarks>
    /// Регистры 1131-1132 (uint32). Уникальный код поставщика оборудования.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с кодом поставщика.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadSupplierCodeAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterSupplierCode - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Код поставщика: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения кода поставщика: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает дату производства.
    /// </summary>
    /// <remarks>
    /// Регистры 1133-1136. ASCII строка из 8 символов (формат: YYYYMMDD или подобный).
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с датой производства.</returns>
    public async Task<DiagnosticReadResult<string>> ReadManufactureDateAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterManufactureDate - _settings.BaseAddressOffset);
        var result = await reader.ReadStringAsync(address, ManufactureDateLength, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Дата производства: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения даты производства: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает серийный номер.
    /// </summary>
    /// <remarks>
    /// Регистры 1137-1138 (uint32). Уникальный серийный номер устройства.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с серийным номером.</returns>
    public async Task<DiagnosticReadResult<uint>> ReadSerialNumberAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterSerialNumber - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt32Async(address, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Серийный номер: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения серийного номера: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает артикул изделия.
    /// </summary>
    /// <remarks>
    /// Регистры 1139-1145. ASCII строка из 14 символов.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с артикулом изделия.</returns>
    public async Task<DiagnosticReadResult<string>> ReadArticleNumberAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterArticleNumber - _settings.BaseAddressOffset);
        var result = await reader.ReadStringAsync(address, ArticleNumberLength, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Артикул изделия: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения артикула изделия: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает артикул котла.
    /// </summary>
    /// <remarks>
    /// Регистры 1175-1181. ASCII строка из 14 символов.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с артикулом котла.</returns>
    public async Task<DiagnosticReadResult<string>> ReadBoilerArticleAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterBoilerArticle - _settings.BaseAddressOffset);
        var result = await reader.ReadStringAsync(address, ArticleNumberLength, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Артикул котла: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения артикула котла: {Error}", result.Error!);
        }

        return result;
    }

    /// <summary>
    /// Читает артикул Ителма.
    /// </summary>
    /// <remarks>
    /// Регистры 1182-1188. ASCII строка из 14 символов.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с артикулом Ителма.</returns>
    public async Task<DiagnosticReadResult<string>> ReadItelmaArticleAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterItelmaArticle - _settings.BaseAddressOffset);
        var result = await reader.ReadStringAsync(address, ArticleNumberLength, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogDebug("Артикул Ителма: {Value}", result.Value);
        }
        else
        {
            _logger.LogError("Ошибка чтения артикула Ителма: {Error}", result.Error!);
        }

        return result;
    }

    #endregion

    #region Errors (reg 1047, 1126-1130)

    /// <summary>
    /// Читает последнюю ошибку котла.
    /// </summary>
    /// <remarks>
    /// Регистр 1047. Возвращает информацию об ошибке с кодом дисплея и описанием.
    /// Значение 0 означает отсутствие ошибки.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с информацией об ошибке.</returns>
    public async Task<DiagnosticReadResult<BoilerErrorInfo>> ReadLastErrorAsync(CancellationToken ct = default)
    {
        var address = (ushort)(RegisterLastError - _settings.BaseAddressOffset);
        var result = await reader.ReadUInt16Async(address, ct).ConfigureAwait(false);

        if (!result.Success)
        {
            _logger.LogError("Ошибка чтения последней ошибки котла: {Error}", result.Error!);
            return DiagnosticReadResult<BoilerErrorInfo>.Fail(address, result.Error!);
        }

        var errorInfo = BoilerErrors.Get(result.Value);
        _logger.LogDebug("Последняя ошибка котла: {ErrorCode} - {Description}", errorInfo.DisplayCode, errorInfo.Description);
        return DiagnosticReadResult<BoilerErrorInfo>.Ok(address, errorInfo);
    }

    /// <summary>
    /// Читает журнал ошибок котла.
    /// </summary>
    /// <remarks>
    /// Регистры 1126-1130. Каждый регистр содержит 2 кода ошибок (по 1 байту).
    /// Возвращает до 10 последних ошибок. Нулевые значения игнорируются.
    /// </remarks>
    /// <param name="ct">Токен отмены операции.</param>
    /// <returns>Результат чтения с массивом информации об ошибках.</returns>
    public async Task<DiagnosticReadResult<BoilerErrorInfo[]>> ReadErrorLogAsync(CancellationToken ct = default)
    {
        var startAddress = (ushort)(RegisterErrorLogStart - _settings.BaseAddressOffset);
        var results = await reader.ReadMultipleUInt16Async(startAddress, ErrorLogRegisterCount, ct).ConfigureAwait(false);

        var errors = ExtractErrorsFromRegisters(results, startAddress);
        return DiagnosticReadResult<BoilerErrorInfo[]>.Ok(startAddress, errors);
    }

    private BoilerErrorInfo[] ExtractErrorsFromRegisters(
        Dictionary<ushort, DiagnosticReadResult<ushort>> results,
        ushort startAddress)
    {
        var errors = new BoilerErrorInfo[MaxErrorsInLog];
        var count = 0;

        for (ushort i = 0; i < ErrorLogRegisterCount; i++)
        {
            var address = (ushort)(startAddress + i);
            count = ExtractErrorsFromRegister(results, address, errors, count);
        }

        return errors[..count];
    }

    private int ExtractErrorsFromRegister(
        Dictionary<ushort, DiagnosticReadResult<ushort>> results,
        ushort address,
        BoilerErrorInfo[] errors,
        int count)
    {
        if (!results.TryGetValue(address, out var result) || !result.Success)
        {
            return count;
        }

        var highByte = (byte)(result.Value >> BitsPerByte);
        var lowByte = (byte)(result.Value & LowByteMask);

        count = AddErrorIfNotZero(highByte, errors, count);
        count = AddErrorIfNotZero(lowByte, errors, count);

        return count;
    }

    private int AddErrorIfNotZero(byte errorCode, BoilerErrorInfo[] errors, int count)
    {
        if (errorCode != 0)
        {
            errors[count++] = BoilerErrors.Get(errorCode);
        }

        return count;
    }

    #endregion
}
