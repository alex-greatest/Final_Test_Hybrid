namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionState
{
    public bool IsCompleted { get; private set; }
    public bool IsInitializing => !IsCompleted;
    public event Action? OnStateChanged;

    public void SetInitializing()
    {
        if (!IsCompleted)
        {
            return;
        }

        IsCompleted = false;
        OnStateChanged?.Invoke();
    }

    public void SetCompleted()
    {
        if (IsCompleted)
        {
            return;
        }

        IsCompleted = true;
        OnStateChanged?.Invoke();
    }
}
