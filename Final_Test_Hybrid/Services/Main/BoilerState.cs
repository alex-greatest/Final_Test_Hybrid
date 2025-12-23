using Final_Test_Hybrid.Models.Database;

namespace Final_Test_Hybrid.Services.Main;

public class BoilerState
{
    private readonly Lock _lock = new();
    private string? _serialNumber;
    private string? _article;
    private bool _isValid;
    private BoilerTypeCycle? _boilerTypeCycle;

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

    public BoilerTypeCycle? BoilerTypeCycle
    {
        get
        {
            lock (_lock)
            {
                return _boilerTypeCycle;
            }
        }
    }

    public void SetData(string serialNumber, string article, bool isValid, BoilerTypeCycle? boilerTypeCycle = null)
    {
        UpdateState(serialNumber, article, isValid, boilerTypeCycle);
        NotifyChanged();
    }

    public void Clear()
    {
        UpdateState(serialNumber: null, article: null, isValid: false, boilerTypeCycle: null);
        NotifyChanged();
    }

    private void UpdateState(string? serialNumber, string? article, bool isValid, BoilerTypeCycle? boilerTypeCycle)
    {
        lock (_lock)
        {
            _serialNumber = serialNumber;
            _article = article;
            _isValid = isValid;
            _boilerTypeCycle = boilerTypeCycle;
        }
    }

    private void NotifyChanged()
    {
        OnChanged?.Invoke();
    }
}
