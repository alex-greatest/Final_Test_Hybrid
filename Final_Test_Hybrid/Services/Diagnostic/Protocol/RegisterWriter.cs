using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Типизированная запись регистров ЭБУ котла.
/// </summary>
public class RegisterWriter(
    IModbusClient modbusClient,
    ILogger<RegisterWriter> logger,
    ITestStepLogger testStepLogger)
{
    private readonly DualLogger<RegisterWriter> _logger = new(logger, testStepLogger);
    /// <summary>
    /// Записывает unsigned 16-bit значение.
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteUInt16Async(ushort address, ushort value, CancellationToken ct = default)
    {
        try
        {
            await modbusClient.WriteSingleRegisterAsync(address, value, CommandPriority.High, ct).ConfigureAwait(false);
            return DiagnosticWriteResult.Ok(address);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка записи в регистр {Address}: {Error}", address, ex.Message);
            return DiagnosticWriteResult.Fail(address, ex.Message);
        }
    }

    /// <summary>
    /// Записывает signed 16-bit значение.
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteInt16Async(ushort address, short value, CancellationToken ct = default)
    {
        try
        {
            await modbusClient.WriteSingleRegisterAsync(address, (ushort)value, CommandPriority.High, ct).ConfigureAwait(false);
            return DiagnosticWriteResult.Ok(address);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка записи в регистр {Address}: {Error}", address, ex.Message);
            return DiagnosticWriteResult.Fail(address, ex.Message);
        }
    }

    /// <summary>
    /// Записывает unsigned 32-bit значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteUInt32Async(ushort addressHi, uint value, CancellationToken ct = default)
    {
        try
        {
            var registers = new ushort[]
            {
                (ushort)(value >> 16),  // Hi word
                (ushort)(value & 0xFFFF) // Lo word
            };

            await modbusClient.WriteMultipleRegistersAsync(addressHi, registers, CommandPriority.High, ct).ConfigureAwait(false);
            return DiagnosticWriteResult.Ok(addressHi);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка записи в регистр {Address}: {Error}", addressHi, ex.Message);
            return DiagnosticWriteResult.Fail(addressHi, ex.Message);
        }
    }

    /// <summary>
    /// Записывает float значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteFloatAsync(ushort addressHi, float value, CancellationToken ct = default)
    {
        try
        {
            var bytes = BitConverter.GetBytes(value);

            // Convert from Little Endian to Big Endian if needed
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            var registers = new[]
            {
                (ushort)((bytes[0] << 8) | bytes[1]), // Hi word
                (ushort)((bytes[2] << 8) | bytes[3])  // Lo word
            };

            await modbusClient.WriteMultipleRegistersAsync(addressHi, registers, CommandPriority.High, ct).ConfigureAwait(false);
            return DiagnosticWriteResult.Ok(addressHi);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка записи в регистр {Address}: {Error}", addressHi, ex.Message);
            return DiagnosticWriteResult.Fail(addressHi, ex.Message);
        }
    }

    /// <summary>
    /// Записывает ASCII строку в регистры (2 символа на регистр, high byte first).
    /// </summary>
    /// <remarks>
    /// Если длина строки меньше maxLength — добавляется терминирующий ноль.
    /// Если длина равна maxLength — без терминатора.
    /// Поддерживаются только ASCII символы (0-127).
    /// </remarks>
    public async Task<DiagnosticWriteResult> WriteStringAsync(
        ushort address, string value, int maxLength, CancellationToken ct = default)
    {
        try
        {
            if (value.Length > maxLength)
            {
                return DiagnosticWriteResult.Fail(address, $"Строка слишком длинная: {value.Length} > {maxLength}");
            }

            if (value.Any(c => c > 127))
            {
                return DiagnosticWriteResult.Fail(address, "Строка содержит не-ASCII символы");
            }

            var registerCount = (maxLength + 1) / 2;
            var registers = new ushort[registerCount];

            for (var i = 0; i < registerCount; i++)
            {
                var charIndex = i * 2;
                var highChar = charIndex < value.Length ? value[charIndex] : '\0';
                var lowChar = charIndex + 1 < value.Length ? value[charIndex + 1] : '\0';
                registers[i] = (ushort)((highChar << 8) | lowChar);
            }

            await modbusClient.WriteMultipleRegistersAsync(address, registers, CommandPriority.High, ct)
                .ConfigureAwait(false);
            return DiagnosticWriteResult.Ok(address);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Ошибка записи строки в регистр {Address}: {Error}", address, ex.Message);
            return DiagnosticWriteResult.Fail(address, ex.Message);
        }
    }
}
