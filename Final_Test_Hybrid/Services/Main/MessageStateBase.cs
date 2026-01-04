namespace Final_Test_Hybrid.Services.Main;

/// <summary>
/// Base class for thread-safe message state management.
/// Provides atomic message updates with change notifications.
/// </summary>
public abstract class MessageStateBase
{
    private readonly Lock _lock = new();
    private string? _message;

    public event Action? OnChange;

    public void SetMessage(string? message)
    {
        lock (_lock)
        {
            if (_message == message) { return; }
            _message = message;
        }
        OnChange?.Invoke();
    }

    public void Clear() => SetMessage(null);

    public string? GetMessage()
    {
        lock (_lock) { return _message; }
    }
}
