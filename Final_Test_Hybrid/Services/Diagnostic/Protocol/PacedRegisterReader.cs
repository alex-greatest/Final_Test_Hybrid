using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Opt-in pacing для Modbus-чтений внутри test-step контекста.
/// </summary>
public class PacedRegisterReader
{
    private readonly PausableRegisterReader _inner;
    private readonly TestStepModbusPacing _pacing;

    internal PacedRegisterReader(PausableRegisterReader inner, TestStepModbusPacing pacing)
    {
        _inner = inner;
        _pacing = pacing;
    }

    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(ushort address, CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadUInt16Async(address, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<ushort>> ReadUInt16Async(
        ushort address,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadUInt16Async(address, priority, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<short>> ReadInt16Async(ushort address, CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadInt16Async(address, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<short>> ReadInt16Async(
        ushort address,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadInt16Async(address, priority, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(ushort addressHi, CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadUInt32Async(addressHi, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<uint>> ReadUInt32Async(
        ushort addressHi,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadUInt32Async(addressHi, priority, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(ushort addressHi, CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadFloatAsync(addressHi, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<float>> ReadFloatAsync(
        ushort addressHi,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadFloatAsync(addressHi, priority, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<string>> ReadStringAsync(
        ushort address,
        int maxLength,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadStringAsync(address, maxLength, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticReadResult<string>> ReadStringAsync(
        ushort address,
        int maxLength,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadStringAsync(address, maxLength, priority, ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress,
        ushort count,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadMultipleUInt16Async(startAddress, count, ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<ushort, DiagnosticReadResult<ushort>>> ReadMultipleUInt16Async(
        ushort startAddress,
        ushort count,
        CommandPriority priority,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.ReadMultipleUInt16Async(startAddress, count, priority, ct).ConfigureAwait(false);
    }
}
