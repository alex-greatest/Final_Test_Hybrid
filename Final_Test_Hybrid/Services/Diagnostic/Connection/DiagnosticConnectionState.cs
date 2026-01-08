namespace Final_Test_Hybrid.Services.Diagnostic.Connection;

/// <summary>
/// Состояние подключения к ЭБУ котла через COM-порт.
/// Thread-safe через lock.
/// </summary>
public class DiagnosticConnectionState
{
    private readonly object _lock = new();
    private TaskCompletionSource _connectionTcs = new();

    /// <summary>
    /// Флаг подключения к устройству.
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Событие изменения состояния подключения.
    /// </summary>
    public event Action<bool>? ConnectionStateChanged;

    /// <summary>
    /// Устанавливает состояние подключения.
    /// </summary>
    public void SetConnected(bool connected)
    {
        UpdateConnectionState(connected);
        NotifyConnectionStateChanged(connected);
    }

    /// <summary>
    /// Ожидает установки подключения.
    /// </summary>
    public Task WaitForConnectionAsync(CancellationToken ct = default)
    {
        lock (_lock)
        {
            return IsConnected
                ? Task.CompletedTask
                : _connectionTcs.Task.WaitAsync(ct);
        }
    }

    private void UpdateConnectionState(bool connected)
    {
        lock (_lock)
        {
            IsConnected = connected;

            if (connected)
            {
                CompleteAndResetConnectionTask();
            }
        }
    }

    private void CompleteAndResetConnectionTask()
    {
        _connectionTcs.TrySetResult();
        _connectionTcs = new TaskCompletionSource();
    }

    private void NotifyConnectionStateChanged(bool connected)
    {
        ConnectionStateChanged?.Invoke(connected);
    }
}