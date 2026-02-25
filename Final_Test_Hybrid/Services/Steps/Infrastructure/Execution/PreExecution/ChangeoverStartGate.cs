using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;

public sealed class ChangeoverStartGate(DualLogger<ChangeoverStartGate> logger) : IChangeoverStartGate
{
    private int _pendingAutoReadyRequest;
    public event Action? OnAutoReadyRequested;

    public void RequestStartFromAutoReady()
    {
        try
        {
            Interlocked.Exchange(ref _pendingAutoReadyRequest, 1);
            OnAutoReadyRequested?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка запуска changeover по AutoReady");
        }
    }

    public bool TryConsumePendingAutoReadyRequest()
    {
        return Interlocked.Exchange(ref _pendingAutoReadyRequest, 0) == 1;
    }
}
