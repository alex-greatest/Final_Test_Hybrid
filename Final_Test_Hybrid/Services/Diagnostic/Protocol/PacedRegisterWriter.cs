using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Diagnostic.Protocol;

/// <summary>
/// Opt-in pacing для Modbus-записей внутри test-step контекста.
/// </summary>
public class PacedRegisterWriter
{
    private readonly PausableRegisterWriter _inner;
    private readonly TestStepModbusPacing _pacing;

    internal PacedRegisterWriter(PausableRegisterWriter inner, TestStepModbusPacing pacing)
    {
        _inner = inner;
        _pacing = pacing;
    }

    public async Task<DiagnosticWriteResult> WriteUInt16Async(
        ushort address,
        ushort value,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.WriteUInt16Async(address, value, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticWriteResult> WriteInt16Async(
        ushort address,
        short value,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.WriteInt16Async(address, value, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticWriteResult> WriteUInt32Async(
        ushort addressHi,
        uint value,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.WriteUInt32Async(addressHi, value, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticWriteResult> WriteFloatAsync(
        ushort addressHi,
        float value,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.WriteFloatAsync(addressHi, value, ct).ConfigureAwait(false);
    }

    public async Task<DiagnosticWriteResult> WriteStringAsync(
        ushort address,
        string value,
        int maxLength,
        CancellationToken ct = default)
    {
        await _pacing.WaitBeforeOperationAsync(ct).ConfigureAwait(false);
        return await _inner.WriteStringAsync(address, value, maxLength, ct).ConfigureAwait(false);
    }
}
