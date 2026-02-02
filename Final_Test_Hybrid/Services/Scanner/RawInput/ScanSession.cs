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
        // Инвариант: желательно одна сессия, но разрешаем перезапись.
        // Overlap возможен при авторизации: диалог держит сессию,
        // SetAuthenticated вызывает ScanModeController до закрытия диалога.
        if (_activeHandler != null)
        {
            System.Diagnostics.Debug.WriteLine(
                "Warning: перезапись активного handler сканера. " +
                "Возможен overlap сессий при авторизации.");
        }

        // Сначала очистка буфера (пока handler == null, символы игнорируются)
        // Это предотвращает race condition когда старые символы попадают в новый handler
        buffer.Clear();
        lock (_lock)
        {
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
        lock (_lock)
        {
            if (_activeHandler != handler)
            {
                return;  // handler уже перезаписан — не трогаем буфер
            }
            _activeHandler = null;
        }
        buffer.Clear();  // очищаем только при реальном освобождении
    }
}
