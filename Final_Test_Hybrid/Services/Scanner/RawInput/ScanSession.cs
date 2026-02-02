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
    private readonly Stack<Action<string>> _handlerStack = new();
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

    // Valid window for keeping buffered characters that arrived before Acquire.
    private static readonly TimeSpan ValidWindow = TimeSpan.FromMilliseconds(200);

    public IDisposable Acquire(Action<string> handler)
    {
        // Overlap is expected: dialogs can temporarily take the scanner.
        if (!buffer.IsWithinValidWindow(ValidWindow))
        {
            buffer.Clear();
        }

        lock (_lock)
        {
            _handlerStack.Push(handler);
            _activeHandler = handler;
        }
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
        bool activeChanged = false;
        lock (_lock)
        {
            if (_activeHandler == null)
            {
                return;
            }

            if (ReferenceEquals(_activeHandler, handler))
            {
                RemoveHandlerUnsafe(handler);
                _activeHandler = _handlerStack.Count > 0 ? _handlerStack.Peek() : null;
                activeChanged = true;
            }
            else
            {
                RemoveHandlerUnsafe(handler);
            }
        }

        if (activeChanged)
        {
            buffer.Clear();
        }
    }

    private void RemoveHandlerUnsafe(Action<string> handler)
    {
        if (_handlerStack.Count == 0)
        {
            return;
        }

        if (ReferenceEquals(_handlerStack.Peek(), handler))
        {
            _handlerStack.Pop();
            return;
        }

        var temp = new Stack<Action<string>>(_handlerStack.Count);
        var removed = false;
        while (_handlerStack.Count > 0)
        {
            var item = _handlerStack.Pop();
            if (!removed && ReferenceEquals(item, handler))
            {
                removed = true;
                continue;
            }
            temp.Push(item);
        }
        while (temp.Count > 0)
        {
            _handlerStack.Push(temp.Pop());
        }
    }
}

