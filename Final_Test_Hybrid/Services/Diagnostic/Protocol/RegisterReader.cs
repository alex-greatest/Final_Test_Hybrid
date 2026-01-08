using System.Text;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Типизированное чтение регистров ЭБУ котла.
/// </summary>
public class RegisterReader(
    ModbusClient modbusClient,
    ILogger<RegisterReader> logger)
{
    /// <summary>
    /// Читает unsigned 16-bit значение.
    /// </summary>
    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(ushort address, CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, 1, ct).ConfigureAwait(false);
            var value = registers[0];

            if (DiagnosticCodes.IsErrorCode(value))
            {
                return DiagnosticReadResult<ushort>.Fail(address, DiagnosticCodes.GetErrorMessage(value));
            }

            return DiagnosticReadResult<ushort>.Ok(address, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка чтения UInt16 по адресу {Address}", address);
            return DiagnosticReadResult<ushort>.Fail(address, ex.Message);
        }
    }

    /// <summary>
    /// Читает signed 16-bit значение.
    /// </summary>
    public async Task<DiagnosticReadResult<short>> ReadInt16Async(ushort address, CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, 1, ct).ConfigureAwait(false);
            var value = (short)registers[0];
            return DiagnosticCodes.IsSignedErrorCode(value) ? DiagnosticReadResult<short>.Fail(address, DiagnosticCodes.GetSignedErrorMessage(value)) : DiagnosticReadResult<short>.Ok(address, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка чтения Int16 по адресу {Address}", address);
            return DiagnosticReadResult<short>.Fail(address, ex.Message);
        }
    }

    /// <summary>
    /// Читает unsigned 32-bit значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(ushort addressHi, CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(addressHi, 2, ct).ConfigureAwait(false);
            var value = ((uint)registers[0] << 16) | registers[1];
            return DiagnosticCodes.IsUInt32ErrorCode(value) ? DiagnosticReadResult<uint>.Fail(addressHi, DiagnosticCodes.GetUInt32ErrorDescription(value)!) : DiagnosticReadResult<uint>.Ok(addressHi, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка чтения UInt32 по адресу {Address}", addressHi);
            return DiagnosticReadResult<uint>.Fail(addressHi, ex.Message);
        }
    }

    /// <summary>
    /// Читает float значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(ushort addressHi, CancellationToken ct = default)
    {
        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(addressHi, 2, ct).ConfigureAwait(false);
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
            return DiagnosticCodes.IsFloatErrorCode(value, out var errorType) ? DiagnosticReadResult<float>.Fail(addressHi, errorType!) : DiagnosticReadResult<float>.Ok(addressHi, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка чтения Float по адресу {Address}", addressHi);
            return DiagnosticReadResult<float>.Fail(addressHi, ex.Message);
        }
    }

    /// <summary>
    /// Читает строку из регистров (2 символа на регистр, null-terminated).
    /// </summary>
    public async Task<DiagnosticReadResult<string>> ReadStringAsync(ushort address, int maxLength, CancellationToken ct = default)
    {
        try
        {
            var registerCount = (ushort)((maxLength + 1) / 2);
            var registers = await modbusClient.ReadHoldingRegistersAsync(address, registerCount, ct).ConfigureAwait(false);
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

            return DiagnosticReadResult<string>.Ok(address, sb.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Ошибка чтения String по адресу {Address}", address);
            return DiagnosticReadResult<string>.Fail(address, ex.Message);
        }
    }

    /// <summary>
    /// Читает несколько UInt16 регистров одним запросом.
    /// </summary>
    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress, ushort count, CancellationToken ct = default)
    {
        var results = new Dictionary<ushort, DiagnosticReadResult<ushort>>();

        try
        {
            var registers = await modbusClient.ReadHoldingRegistersAsync(startAddress, count, ct).ConfigureAwait(false);
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
            logger.LogError(ex, "Ошибка чтения нескольких UInt16 с адреса {Address}", startAddress);

            for (var i = 0; i < count; i++)
            {
                var address = (ushort)(startAddress + i);
                results[address] = DiagnosticReadResult<ushort>.Fail(address, ex.Message);
            }
        }
        return results;
    }
}
