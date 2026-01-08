namespace Final_Test_Hybrid.Services.Scanner.RawInput;

/// <summary>
/// Represents an active barcode scanning session.
/// Disposing the session clears the handler and buffer.
/// </summary>
internal sealed class ScanSession : IDisposable
{
    private readonly Action _onDispose;
    private bool _disposed;

    internal ScanSession(Action onDispose)
    {
        _onDispose = onDispose;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _onDispose();
    }
}

/// <summary>
/// Manages the active barcode handler with thread-safe operations.
/// </summary>
public sealed class ScanSessionHandler(BarcodeBuffer buffer)
{
    private readonly Lock _lock = new();
    private Action<string>? _activeHandler;

    public bool HasActiveHandler
    {
        get
        {
            lock (_lock)
            {
                return _activeHandler != null;
            }
        }
    }

    public IDisposable Acquire(Action<string> handler)
    {
        lock (_lock)
        {
            _activeHandler = handler;
        }
        buffer.Clear();
        return new ScanSession(() => Release(handler));
    }

    public Action<string>? GetHandler()
    {
        lock (_lock)
        {
            return _activeHandler;
        }
    }

    private void Release(Action<string> handler)
    {
        lock (_lock)
        {
            if (_activeHandler == handler)
            {
                _activeHandler = null;
            }
        }
        buffer.Clear();
    }
}
