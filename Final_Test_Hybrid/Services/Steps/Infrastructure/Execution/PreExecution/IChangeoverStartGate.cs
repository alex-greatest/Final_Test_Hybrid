namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public interface IChangeoverStartGate
{
    event Action? OnAutoReadyRequested;
    void RequestStartFromAutoReady();
    bool TryConsumePendingAutoReadyRequest();
}
