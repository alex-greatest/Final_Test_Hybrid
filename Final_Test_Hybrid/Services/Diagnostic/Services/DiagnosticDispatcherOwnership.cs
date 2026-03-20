using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;

namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Координирует ownership shared ModbusDispatcher между UI-панелями и runtime.
/// </summary>
public sealed class DiagnosticDispatcherOwnership : IDisposable
{
    private readonly IModbusDispatcher _dispatcher;
    private readonly Lock _sync = new();
    private int _panelLeaseCount;
    private int _runtimeLeaseCount;
    private bool _runtimePersistent;
    private bool _trackedSessionActive;
    private bool _disposed;

    public DiagnosticDispatcherOwnership(IModbusDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _dispatcher.Stopped += OnDispatcherStopped;
    }

    /// <summary>
    /// Берёт lease для панели ручной диагностики.
    /// </summary>
    public DiagnosticDispatcherLease AcquirePanelLease()
    {
        return AcquireLease(DiagnosticDispatcherLeaseKind.Panel);
    }

    /// <summary>
    /// Берёт временный lease для runtime-шага.
    /// </summary>
    public DiagnosticDispatcherLease AcquireRuntimeLease()
    {
        return AcquireLease(DiagnosticDispatcherLeaseKind.Runtime);
    }

    private DiagnosticDispatcherLease AcquireLease(DiagnosticDispatcherLeaseKind kind)
    {
        lock (_sync)
        {
            ThrowIfDisposed();

            var shouldStartDispatcher = ShouldStartDispatcherUnsafe();
            if (shouldStartDispatcher)
            {
                _trackedSessionActive = true;
            }

            IncrementLeaseUnsafe(kind);
            return new DiagnosticDispatcherLease(this, kind, shouldStartDispatcher);
        }
    }

    private bool ShouldStartDispatcherUnsafe()
    {
        return !_dispatcher.IsStarted
               && !_trackedSessionActive
               && !_runtimePersistent
               && _runtimeLeaseCount == 0;
    }

    private void IncrementLeaseUnsafe(DiagnosticDispatcherLeaseKind kind)
    {
        switch (kind)
        {
            case DiagnosticDispatcherLeaseKind.Panel:
                _panelLeaseCount++;
                break;
            case DiagnosticDispatcherLeaseKind.Runtime:
                _runtimeLeaseCount++;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private void DecrementLeaseUnsafe(DiagnosticDispatcherLeaseKind kind)
    {
        switch (kind)
        {
            case DiagnosticDispatcherLeaseKind.Panel:
                _panelLeaseCount = Math.Max(0, _panelLeaseCount - 1);
                break;
            case DiagnosticDispatcherLeaseKind.Runtime:
                _runtimeLeaseCount = Math.Max(0, _runtimeLeaseCount - 1);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }

    private bool HasActiveOwnershipUnsafe()
    {
        return _panelLeaseCount > 0 || _runtimeLeaseCount > 0 || _runtimePersistent;
    }

    internal DiagnosticDispatcherRelease Release(DiagnosticDispatcherLeaseKind kind)
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return DiagnosticDispatcherRelease.None;
            }

            DecrementLeaseUnsafe(kind);

            if (!_dispatcher.IsStarted && !HasActiveOwnershipUnsafe())
            {
                _trackedSessionActive = false;
            }

            return new DiagnosticDispatcherRelease(ShouldStopDispatcherUnsafe());
        }
    }

    internal void PromoteRuntimeOwnership()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_runtimeLeaseCount > 0)
            {
                _runtimeLeaseCount--;
            }

            _runtimePersistent = true;
            _trackedSessionActive = true;
        }
    }

    private bool ShouldStopDispatcherUnsafe()
    {
        return _trackedSessionActive
               && !HasActiveOwnershipUnsafe()
               && _dispatcher.IsStarted;
    }

    private void OnDispatcherStopped()
    {
        lock (_sync)
        {
            _runtimePersistent = false;
            _trackedSessionActive = false;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _dispatcher.Stopped -= OnDispatcherStopped;
    }
}

/// <summary>
/// Lease на shared dispatcher.
/// </summary>
public sealed class DiagnosticDispatcherLease
{
    private readonly DiagnosticDispatcherOwnership _owner;
    private readonly DiagnosticDispatcherLeaseKind _kind;
    private int _completed;

    internal DiagnosticDispatcherLease(
        DiagnosticDispatcherOwnership owner,
        DiagnosticDispatcherLeaseKind kind,
        bool shouldStartDispatcher)
    {
        _owner = owner;
        _kind = kind;
        ShouldStartDispatcher = shouldStartDispatcher;
    }

    /// <summary>
    /// True, если caller должен стартовать dispatcher.
    /// </summary>
    public bool ShouldStartDispatcher { get; }

    /// <summary>
    /// Переводит временный runtime-lease в sticky ownership до следующего StopAsync().
    /// </summary>
    public void PromoteToPersistentRuntimeOwnership()
    {
        if (_kind != DiagnosticDispatcherLeaseKind.Runtime)
        {
            throw new InvalidOperationException("Persistent runtime ownership доступен только runtime-lease.");
        }

        if (Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _owner.PromoteRuntimeOwnership();
    }

    /// <summary>
    /// Освобождает lease и возвращает решение, нужно ли останавливать dispatcher.
    /// </summary>
    public DiagnosticDispatcherRelease Release()
    {
        return Interlocked.Exchange(ref _completed, 1) != 0
            ? DiagnosticDispatcherRelease.None
            : _owner.Release(_kind);
    }
}

public readonly record struct DiagnosticDispatcherRelease(bool ShouldStopDispatcher)
{
    public static DiagnosticDispatcherRelease None => new(false);
}

internal enum DiagnosticDispatcherLeaseKind
{
    Panel,
    Runtime
}
