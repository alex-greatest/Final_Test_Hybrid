using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator.Behaviors;

/// <summary>
/// Context provided to interrupt behavior strategies.
/// </summary>
public interface IInterruptContext
{
    void Pause();
    void Reset();
    IErrorService ErrorService { get; }
    INotificationService Notifications { get; }
}
