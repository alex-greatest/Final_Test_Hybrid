using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Models;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Pausable-обёртка над RegisterWriter для использования в тестовых шагах.
/// Блокирует операции при паузе теста (Auto OFF).
/// </summary>
public class PausableRegisterWriter(RegisterWriter inner, PauseTokenSource pauseToken)
{
    /// <summary>
    /// Записывает unsigned 16-bit значение.
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteUInt16Async(
        ushort address, ushort value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteUInt16Async(address, value, ct);
    }

    /// <summary>
    /// Записывает signed 16-bit значение.
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteInt16Async(
        ushort address, short value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteInt16Async(address, value, ct);
    }

    /// <summary>
    /// Записывает unsigned 32-bit значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteUInt32Async(
        ushort addressHi, uint value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteUInt32Async(addressHi, value, ct);
    }

    /// <summary>
    /// Записывает float значение (Big Endian: Hi регистр первый).
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteFloatAsync(
        ushort addressHi, float value, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteFloatAsync(addressHi, value, ct);
    }

    /// <summary>
    /// Записывает ASCII строку в регистры (2 символа на регистр, high byte first).
    /// </summary>
    public async Task<DiagnosticWriteResult> WriteStringAsync(
        ushort address, string value, int maxLength, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.WriteStringAsync(address, value, maxLength, ct);
    }
}
