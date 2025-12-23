namespace Final_Test_Hybrid.Services.Main;

public class BoilerState
{
    private readonly Lock _lock = new();
    private string? _serialNumber;
    private string? _article;
    private bool _isValid;
    public string? SerialNumber { get { lock (_lock) return _serialNumber; } }
    public string? Article { get { lock (_lock) return _article; } }
    public bool IsValid { get { lock (_lock) return _isValid; } }

    public event Action? OnChanged;

    public void SetData(string serialNumber, string article, bool isValid = true)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
            _isValid = isValid;
        }
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        lock (_lock)
        {
            _serialNumber = null;
            _article = null;
            _isValid = false;
        }
        OnChanged?.Invoke();
    }
}
