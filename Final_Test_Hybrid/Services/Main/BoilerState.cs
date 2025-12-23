namespace Final_Test_Hybrid.Services.Main;

public class BoilerState
{
    private readonly Lock _lock = new();
    private string? _serialNumber;
    private string? _article;
    public string? SerialNumber { get { lock (_lock) return _serialNumber; } }
    public string? Article { get { lock (_lock) return _article; } }

    public event Action? OnChanged;

    public void SetData(string serialNumber, string article)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _serialNumber = null;
            _article = null;
        }
        OnChanged?.Invoke();
    }
}
