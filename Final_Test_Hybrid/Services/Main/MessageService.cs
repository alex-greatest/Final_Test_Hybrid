namespace Final_Test_Hybrid.Services.Main;

public class MessageService
{
    private readonly List<(int priority, Func<string?> provider)> _providers = [];
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

    public string CurrentMessage =>
        _isSuppressed
            ? ""
            : _providers
                .OrderByDescending(x => x.priority)
                .Select(x => x.provider())
                .FirstOrDefault(m => m != null) ?? "";

    public void RegisterProvider(int priority, Func<string?> provider)
    {
        _providers.Add((priority, provider));
    }

    public void NotifyChanged() => OnChange?.Invoke();
}
