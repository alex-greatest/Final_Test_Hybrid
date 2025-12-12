namespace Final_Test_Hybrid.Services.SpringBoot.Health;

public class SpringBootConnectionState
{
    private readonly Lock _lock = new();
    public bool IsConnected { get; private set; }
    public event Action<bool>? ConnectionStateChanged;

    public void SetConnected(bool connected)
    {
        lock (_lock)
        {
            if (IsConnected == connected)
            {
                return;
            }
            IsConnected = connected;
        }
        ConnectionStateChanged?.Invoke(connected);
    }
}
