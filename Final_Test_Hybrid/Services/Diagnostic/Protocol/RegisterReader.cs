using System.Text;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Типизированное чтение регистров ЭБУ котла.
/// </summary>
public class RegisterReader(
    IModbusClient modbusClient,
    ILogger<RegisterReader> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<RegisterReader> _logger = new(logger, testStepLogger);

    #region ReadUInt16

    /// <summary>
    /// Читает unsigned 16-bit значение с высоким приоритетом.
    /// </summary>
    /// <param name="address">Адрес регистра.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<DiagnosticReadResult<ushort>> ReadUInt16Async(ushort address, CancellationToken ct = default)
        => ReadUInt16Async(address, CommandPriority.High, ct);

    /// <summary>
    /// Читает unsigned 16-bit значение с указанным приоритетом.
    /// </summary>
    /// <param name="address">Адрес регистра.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(
        ushort address,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, 1, priority, ct).ConfigureAwait(false);
            var value = registers[0];

            if (DiagnosticCodes.IsErrorCode(value))
            {
                return DiagnosticReadResult<ushort>.Fail(address, DiagnosticCodes.GetErrorMessage(value));
            }

            _logger.LogDebug("Чтение регистра {Address}: {Value}", address, value);
            return DiagnosticReadResult<ushort>.Ok(address, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения регистра {Address}: {Error}", address, ex.Message);
            return DiagnosticReadResult<ushort>.Fail(address, ex.Message);
        }
    }

    #endregion

    #region ReadInt16

    /// <summary>
    /// Читает signed 16-bit значение с высоким приоритетом.
    /// </summary>
    /// <param name="address">Адрес регистра.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<DiagnosticReadResult<short>> ReadInt16Async(ushort address, CancellationToken ct = default)
        => ReadInt16Async(address, CommandPriority.High, ct);

    /// <summary>
    /// Читает signed 16-bit значение с указанным приоритетом.
    /// </summary>
    /// <param name="address">Адрес регистра.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<DiagnosticReadResult<short>> ReadInt16Async(
        ushort address,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, 1, priority, ct).ConfigureAwait(false);
            var value = (short)registers[0];

            if (DiagnosticCodes.IsSignedErrorCode(value))
            {
                return DiagnosticReadResult<short>.Fail(address, DiagnosticCodes.GetSignedErrorMessage(value));
            }

            _logger.LogDebug("Чтение регистра {Address}: {Value}", address, value);
            return DiagnosticReadResult<short>.Ok(address, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения регистра {Address}: {Error}", address, ex.Message);
            return DiagnosticReadResult<short>.Fail(address, ex.Message);
        }
    }

    #endregion

    #region ReadUInt32

    /// <summary>
    /// Читает unsigned 32-bit значение с высоким приоритетом (Big Endian: Hi регистр первый).
    /// </summary>
    /// <param name="addressHi">Адрес старшего регистра.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<DiagnosticReadResult<uint>> ReadUInt32Async(ushort addressHi, CancellationToken ct = default)
        => ReadUInt32Async(addressHi, CommandPriority.High, ct);

    /// <summary>
    /// Читает unsigned 32-bit значение с указанным приоритетом (Big Endian: Hi регистр первый).
    /// </summary>
    /// <param name="addressHi">Адрес старшего регистра.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(
        ushort addressHi,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(addressHi, 2, priority, ct).ConfigureAwait(false);
            var value = ((uint)registers[0] << 16) | registers[1];

            if (DiagnosticCodes.IsUInt32ErrorCode(value))
            {
                return DiagnosticReadResult<uint>.Fail(addressHi, DiagnosticCodes.GetUInt32ErrorDescription(value)!);
            }

            _logger.LogDebug("Чтение регистра {Address}: {Value}", addressHi, value);
            return DiagnosticReadResult<uint>.Ok(addressHi, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения регистра {Address}: {Error}", addressHi, ex.Message);
            return DiagnosticReadResult<uint>.Fail(addressHi, ex.Message);
        }
    }

    #endregion

    #region ReadFloat

    /// <summary>
    /// Читает float значение с высоким приоритетом (Big Endian: Hi регистр первый).
    /// </summary>
    /// <param name="addressHi">Адрес старшего регистра.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<DiagnosticReadResult<float>> ReadFloatAsync(ushort addressHi, CancellationToken ct = default)
        => ReadFloatAsync(addressHi, CommandPriority.High, ct);

    /// <summary>
    /// Читает float значение с указанным приоритетом (Big Endian: Hi регистр первый).
    /// </summary>
    /// <param name="addressHi">Адрес старшего регистра.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(
        ushort addressHi,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(addressHi, 2, priority, ct).ConfigureAwait(false);
            var bytes = new byte[4];
            // Big Endian: Hi word first
            bytes[0] = (byte)(registers[0] >> 8);
            bytes[1] = (byte)(registers[0] & 0xFF);
            bytes[2] = (byte)(registers[1] >> 8);
            bytes[3] = (byte)(registers[1] & 0xFF);
            // Convert to Little Endian for BitConverter
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            var value = BitConverter.ToSingle(bytes, 0);

            if (DiagnosticCodes.IsFloatErrorCode(value, out var errorType))
            {
                return DiagnosticReadResult<float>.Fail(addressHi, errorType!);
            }

            _logger.LogDebug("Чтение регистра {Address}: {Value}", addressHi, value);
            return DiagnosticReadResult<float>.Ok(addressHi, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения регистра {Address}: {Error}", addressHi, ex.Message);
            return DiagnosticReadResult<float>.Fail(addressHi, ex.Message);
        }
    }

    #endregion

    #region ReadString

    /// <summary>
    /// Читает строку из регистров с высоким приоритетом (2 символа на регистр, null-terminated).
    /// </summary>
    /// <param name="address">Начальный адрес.</param>
    /// <param name="maxLength">Максимальная длина строки.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<DiagnosticReadResult<string>> ReadStringAsync(ushort address, int maxLength, CancellationToken ct = default)
        => ReadStringAsync(address, maxLength, CommandPriority.High, ct);

    /// <summary>
    /// Читает строку из регистров с указанным приоритетом (2 символа на регистр, null-terminated).
    /// </summary>
    /// <param name="address">Начальный адрес.</param>
    /// <param name="maxLength">Максимальная длина строки.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<DiagnosticReadResult<string>> ReadStringAsync(
        ushort address,
        int maxLength,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        try
        {
            var registerCount = (ushort)((maxLength + 1) / 2);
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, registerCount, priority, ct).ConfigureAwait(false);
            var sb = new StringBuilder();
            foreach (var reg in registers)
            {
                var highByte = (char)(reg >> 8);
                var lowByte = (char)(reg & 0xFF);
                if (highByte == '\0')
                {
                    break;
                }
                sb.Append(highByte);
                if (lowByte == '\0')
                {
                    break;
                }
                sb.Append(lowByte);
            }

            var value = sb.ToString();
            _logger.LogDebug("Чтение строки по адресу {Address}: {Value}", address, value);
            return DiagnosticReadResult<string>.Ok(address, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения строки по адресу {Address}: {Error}", address, ex.Message);
            return DiagnosticReadResult<string>.Fail(address, ex.Message);
        }
    }

    #endregion

    #region ReadMultipleUInt16

    /// <summary>
    /// Читает несколько UInt16 регистров одним запросом с высоким приоритетом.
    /// </summary>
    /// <param name="startAddress">Начальный адрес.</param>
    /// <param name="count">Количество регистров.</param>
    /// <param name="ct">Токен отмены.</param>
    public Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress, ushort count, CancellationToken ct = default)
        => ReadMultipleUInt16Async(startAddress, count, CommandPriority.High, ct);

    /// <summary>
    /// Читает несколько UInt16 регистров одним запросом с указанным приоритетом.
    /// </summary>
    /// <param name="startAddress">Начальный адрес.</param>
    /// <param name="count">Количество регистров.</param>
    /// <param name="priority">Приоритет команды.</param>
    /// <param name="ct">Токен отмены.</param>
    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress,
        ushort count,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        var results = new Dictionary<ushort, DiagnosticReadResult<ushort>>();

        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(startAddress, count, priority, ct).ConfigureAwait(false);
            for (var i = 0; i < count; i++)
            {
                var address = (ushort)(startAddress + i);
                var value = registers[i];

                results[address] = DiagnosticCodes.IsErrorCode(value)
                    ? DiagnosticReadResult<ushort>.Fail(address, DiagnosticCodes.GetErrorMessage(value))
                    : DiagnosticReadResult<ushort>.Ok(address, value);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка чтения нескольких регистров с адреса {Address}: {Error}", startAddress, ex.Message);

            for (var i = 0; i < count; i++)
            {
                var address = (ushort)(startAddress + i);
                results[address] = DiagnosticReadResult<ushort>.Fail(address, ex.Message);
            }
        }
        return results;
    }

    #endregion
}
