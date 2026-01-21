using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Pausable-обёртка над RegisterReader для использования в тестовых шагах.
/// Блокирует операции при паузе теста (Auto OFF).
/// </summary>
public class PausableRegisterReader(RegisterReader inner, PauseTokenSource pauseToken)
{
    #region ReadUInt16

    /// <summary>
    /// Читает unsigned 16-bit значение с высоким приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(ushort address, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadUInt16Async(address, ct);
    }

    /// <summary>
    /// Читает unsigned 16-bit значение с указанным приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(
        ushort address, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadUInt16Async(address, priority, ct);
    }

    #endregion

    #region ReadInt16

    /// <summary>
    /// Читает signed 16-bit значение с высоким приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<short>> ReadInt16Async(ushort address, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadInt16Async(address, ct);
    }

    /// <summary>
    /// Читает signed 16-bit значение с указанным приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<short>> ReadInt16Async(
        ushort address, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadInt16Async(address, priority, ct);
    }

    #endregion

    #region ReadUInt32

    /// <summary>
    /// Читает unsigned 32-bit значение с высоким приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(ushort addressHi, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadUInt32Async(addressHi, ct);
    }

    /// <summary>
    /// Читает unsigned 32-bit значение с указанным приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(
        ushort addressHi, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadUInt32Async(addressHi, priority, ct);
    }

    #endregion

    #region ReadFloat

    /// <summary>
    /// Читает float значение с высоким приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(ushort addressHi, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadFloatAsync(addressHi, ct);
    }

    /// <summary>
    /// Читает float значение с указанным приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(
        ushort addressHi, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadFloatAsync(addressHi, priority, ct);
    }

    #endregion

    #region ReadString

    /// <summary>
    /// Читает строку из регистров с высоким приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<string>> ReadStringAsync(
        ushort address, int maxLength, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadStringAsync(address, maxLength, ct);
    }

    /// <summary>
    /// Читает строку из регистров с указанным приоритетом.
    /// </summary>
    public async Task<DiagnosticReadResult<string>> ReadStringAsync(
        ushort address, int maxLength, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadStringAsync(address, maxLength, priority, ct);
    }

    #endregion

    #region ReadMultipleUInt16

    /// <summary>
    /// Читает несколько UInt16 регистров одним запросом с высоким приоритетом.
    /// </summary>
    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress, ushort count, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadMultipleUInt16Async(startAddress, count, ct);
    }

    /// <summary>
    /// Читает несколько UInt16 регистров одним запросом с указанным приоритетом.
    /// </summary>
    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress, ushort count, CommandPriority priority, CancellationToken ct = default)
    {
        await pauseToken.WaitWhilePausedAsync(ct);
        return await inner.ReadMultipleUInt16Async(startAddress, count, priority, ct);
    }

    #endregion
}
