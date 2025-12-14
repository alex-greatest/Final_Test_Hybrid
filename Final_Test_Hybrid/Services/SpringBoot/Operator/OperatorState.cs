namespace Final_Test_Hybrid.Services.SpringBoot.Operator;

public class OperatorState
{
    private readonly Lock _lock = new();
    private bool _isAuthenticated;
    private string? _username;
    private string? _role;
    public bool IsAuthenticated { get { lock (_lock) return _isAuthenticated; } }
    public string? Username { get { lock (_lock) return _username; } }
    public string? Role { get { lock (_lock) return _role; } }
    public event Action? OnChange;

    public void SetAuthenticated(OperatorAuthResponse response)
    {
        lock (_lock)
        {
            _isAuthenticated = true;
            _username = response.Username;
            _role = response.Role;
        }
        OnChange?.Invoke();
    }

    public void Logout()
    {
        lock (_lock)
        {
            _isAuthenticated = false;
            _username = null;
            _role = null;
        }
        OnChange?.Invoke();
    }

    public void SetManualAuth(string username)
    {
        lock (_lock)
        {
            _isAuthenticated = true;
            _username = username;
            _role = null;
        }
        OnChange?.Invoke();
    }
}
