using Final_Test_Hybrid.Services.Scanner.RawInput;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

public enum ScannerInputOwnerKind
{
    None,
    PreExecution,
    Dialog
}

public readonly record struct ScannerInputOwnerState(
    ScannerInputOwnerKind CurrentOwner,
    bool HasPreExecutionOwner,
    int DialogOwnerCount);

public sealed class ScannerInputOwnershipService : IDisposable
{
    private readonly Lock _stateLock = new();
    private readonly IDisposable _rawInputLease;
    private readonly List<DialogScannerOwner> _dialogOwners = [];
    private readonly ILogger<ScannerInputOwnershipService> _logger;
    private Action<string>? _preExecutionHandler;
    private bool _isPreExecutionOwnerActive;
    private bool _disposed;

    public ScannerInputOwnershipService(
        RawInputService rawInputService,
        ILogger<ScannerInputOwnershipService> logger)
    {
        _logger = logger;
        _rawInputLease = rawInputService.RequestScan(HandleBarcodeFromRawInput);
    }

    public event Action? OnStateChanged;

    public ScannerInputOwnerState GetCurrentOwnerState()
    {
        lock (_stateLock)
        {
            return CreateStateUnsafe();
        }
    }

    public bool IsPreExecutionOwnerActive
    {
        get
        {
            lock (_stateLock)
            {
                return GetEffectiveOwnerUnsafe() == ScannerInputOwnerKind.PreExecution;
            }
        }
    }

    public void EnsurePreExecutionOwner(Action<string> handler)
    {
        ScannerInputOwnerKind previousOwner;
        ScannerInputOwnerState nextState;
        bool changed;

        lock (_stateLock)
        {
            ThrowIfDisposed();
            previousOwner = GetEffectiveOwnerUnsafe();
            changed = !_isPreExecutionOwnerActive || !ReferenceEquals(_preExecutionHandler, handler);
            _preExecutionHandler = handler;
            _isPreExecutionOwnerActive = true;
            nextState = CreateStateUnsafe();
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, nextState);
        OnStateChanged?.Invoke();
    }

    public void ReleasePreExecutionOwner()
    {
        ScannerInputOwnerKind previousOwner;
        ScannerInputOwnerState nextState;
        bool changed;

        lock (_stateLock)
        {
            if (_disposed || !_isPreExecutionOwnerActive)
            {
                return;
            }

            previousOwner = GetEffectiveOwnerUnsafe();
            _preExecutionHandler = null;
            _isPreExecutionOwnerActive = false;
            changed = true;
            nextState = CreateStateUnsafe();
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, nextState);
        OnStateChanged?.Invoke();
    }

    public void AcquireDialogOwner(string dialogKey, Action<string> handler)
    {
        ScannerInputOwnerKind previousOwner;
        ScannerInputOwnerState nextState;
        bool changed;

        lock (_stateLock)
        {
            ThrowIfDisposed();
            previousOwner = GetEffectiveOwnerUnsafe();
            RemoveDialogOwnerUnsafe(dialogKey);
            _dialogOwners.Add(new DialogScannerOwner(dialogKey, handler));
            changed = true;
            nextState = CreateStateUnsafe();
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, nextState);
        OnStateChanged?.Invoke();
    }

    public void ReleaseDialogOwner(string dialogKey)
    {
        ScannerInputOwnerKind previousOwner;
        ScannerInputOwnerState nextState;
        bool changed;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            previousOwner = GetEffectiveOwnerUnsafe();
            changed = RemoveDialogOwnerUnsafe(dialogKey);
            nextState = CreateStateUnsafe();
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, nextState);
        OnStateChanged?.Invoke();
    }

    public void ReleaseAllForReset()
    {
        ScannerInputOwnerKind previousOwner;
        bool changed;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            previousOwner = GetEffectiveOwnerUnsafe();
            changed = _dialogOwners.Count > 0 || _isPreExecutionOwnerActive;
            _dialogOwners.Clear();
            _preExecutionHandler = null;
            _isPreExecutionOwnerActive = false;
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, GetCurrentOwnerState());
        OnStateChanged?.Invoke();
    }

    public void ReleaseDialogOwners()
    {
        ScannerInputOwnerKind previousOwner;
        ScannerInputOwnerState nextState;
        bool changed;

        lock (_stateLock)
        {
            if (_disposed || _dialogOwners.Count == 0)
            {
                return;
            }

            previousOwner = GetEffectiveOwnerUnsafe();
            _dialogOwners.Clear();
            changed = true;
            nextState = CreateStateUnsafe();
        }

        if (!changed)
        {
            return;
        }

        LogOwnerChanged(previousOwner, nextState);
        OnStateChanged?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _rawInputLease.Dispose();
    }

    private void HandleBarcodeFromRawInput(string barcode)
    {
        Action<string>? handler;
        ScannerInputOwnerKind ownerKind;
        ScannerInputOwnerState state;

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            handler = GetEffectiveHandlerUnsafe();
            ownerKind = GetEffectiveOwnerUnsafe();
            state = CreateStateUnsafe();
        }

        if (handler == null)
        {
            _logger.LogWarning(
                "barcode_rejected_no_owner: dialogOwners={DialogOwners}, hasPreExecutionOwner={HasPreExecutionOwner}",
                state.DialogOwnerCount,
                state.HasPreExecutionOwner);
            return;
        }

        _logger.LogDebug("barcode_dispatched_to_owner: owner={Owner}", ownerKind);
        handler(barcode);
    }

    private Action<string>? GetEffectiveHandlerUnsafe()
    {
        if (_dialogOwners.Count > 0)
        {
            return _dialogOwners[^1].Handler;
        }

        return _isPreExecutionOwnerActive ? _preExecutionHandler : null;
    }

    private ScannerInputOwnerKind GetEffectiveOwnerUnsafe()
    {
        if (_dialogOwners.Count > 0)
        {
            return ScannerInputOwnerKind.Dialog;
        }

        return _isPreExecutionOwnerActive ? ScannerInputOwnerKind.PreExecution : ScannerInputOwnerKind.None;
    }

    private ScannerInputOwnerState CreateStateUnsafe()
    {
        return new ScannerInputOwnerState(
            GetEffectiveOwnerUnsafe(),
            _isPreExecutionOwnerActive,
            _dialogOwners.Count);
    }

    private bool RemoveDialogOwnerUnsafe(string dialogKey)
    {
        var removed = false;
        for (var index = _dialogOwners.Count - 1; index >= 0; index--)
        {
            if (!_dialogOwners[index].Key.Equals(dialogKey, StringComparison.Ordinal))
            {
                continue;
            }

            _dialogOwners.RemoveAt(index);
            removed = true;
            break;
        }

        return removed;
    }

    private void LogOwnerChanged(
        ScannerInputOwnerKind previousOwner,
        ScannerInputOwnerState nextState)
    {
        _logger.LogInformation(
            "scanner_owner_changed: from={PreviousOwner}, to={CurrentOwner}, dialogOwners={DialogOwners}, hasPreExecutionOwner={HasPreExecutionOwner}",
            previousOwner,
            nextState.CurrentOwner,
            nextState.DialogOwnerCount,
            nextState.HasPreExecutionOwner);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed record DialogScannerOwner(string Key, Action<string> Handler);
}
