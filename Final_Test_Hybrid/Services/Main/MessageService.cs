namespace Final_Test_Hybrid.Services.Main;

/// <summary>
/// Priority-based message provider service.
/// Higher priority providers take precedence when returning non-null messages.
/// </summary>
public class MessageService
{
    private readonly Lock _lock = new();
    private readonly Dictionary<object, (int priority, Func<string?> provider)> _providers = [];
    private volatile bool _isSuppressed;

    public event Action? OnChange;

    public bool IsSuppressed
    {
        get => _isSuppressed;
        set
        {
            _isSuppressed = value;
            NotifyChanged();
        }
    }

    public string CurrentMessage
    {
        get
        {
            if (_isSuppressed)
            {
                return "";
            }
            lock (_lock)
            {
                return _providers.Values
                    .OrderByDescending(x => x.priority)
                    .Select(x => x.provider())
                    .FirstOrDefault(m => m != null) ?? "";
            }
        }
    }

    /// <summary>
    /// Registers a message provider with specified priority.
    /// Returns a key that must be used to unregister the provider.
    /// </summary>
    public object RegisterProvider(int priority, Func<string?> provider)
    {
        var key = new object();
        lock (_lock)
        {
            _providers[key] = (priority, provider);
        }
        return key;
    }

    /// <summary>
    /// Unregisters a previously registered provider.
    /// </summary>
    public void UnregisterProvider(object key)
    {
        lock (_lock)
        {
            _providers.Remove(key);
        }
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
