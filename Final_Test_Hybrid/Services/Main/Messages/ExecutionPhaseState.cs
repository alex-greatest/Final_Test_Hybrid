namespace Final_Test_Hybrid.Services.Main.Messages;

public class ExecutionPhaseState
{
    public ExecutionPhase? Phase { get; private set; }
    public event Action? OnChanged;

    public void SetPhase(ExecutionPhase phase)
    {
        Phase = phase;
        OnChanged?.Invoke();
    }

    public void Clear()
    {
        Phase = null;
        OnChanged?.Invoke();
    }
}
