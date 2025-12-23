namespace Final_Test_Hybrid.Services.Main;

public class BoilerState
{
    private readonly Lock _lock = new();
    private string? _serialNumber;
    private string? _article;
    private bool _isValid;

    public event Action? OnChanged;

    public string? SerialNumber
    {
        get
        {
            lock (_lock)
            {
                return _serialNumber;
            }
        }
    }

    public string? Article
    {
        get
        {
            lock (_lock)
            {
                return _article;
            }
        }
    }

    public bool IsValid
    {
        get
        {
            lock (_lock)
            {
                return _isValid;
            }
        }
    }

    public void SetData(string serialNumber, string article, bool isValid = true)
    {
        UpdateState(serialNumber, article, isValid);
        NotifyChanged();
    }

    public void Clear()
    {
        UpdateState(serialNumber: null, article: null, isValid: false);
        NotifyChanged();
    }

    private void UpdateState(string? serialNumber, string? article, bool isValid)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
            _isValid = isValid;
        }
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
