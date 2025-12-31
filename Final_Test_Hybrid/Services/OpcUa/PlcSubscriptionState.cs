namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionState
{
    public bool IsCompleted { get; private set; }
    public bool IsInitializing => !IsCompleted;
    public event Action? OnStateChanged;

    public void SetCompleted()
    {
        IsCompleted = true;
        OnStateChanged?.Invoke();
    }
}
