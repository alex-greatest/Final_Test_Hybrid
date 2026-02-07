namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionState
{
    public bool IsCompleted { get; private set; } = true;
    public bool IsInitializing { get; private set; }
    public event Action? OnStateChanged;

    public void SetInitializing()
    {
        if (IsInitializing)
        {
            return;
        }

        IsInitializing = true;
        IsCompleted = false;
        OnStateChanged?.Invoke();
    }

    public void SetCompleted()
    {
        if (!IsInitializing)
        {
            return;
        }

        IsInitializing = false;
        IsCompleted = true;
        OnStateChanged?.Invoke();
    }
}
