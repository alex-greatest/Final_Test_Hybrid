using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;

/// <summary>
/// Подписки на состояния для ErrorCoordinator.
/// </summary>
public sealed class ErrorCoordinatorSubscriptions(
    OpcUaConnectionState connectionState,
    AutoReadySubscription autoReady,
    ExecutionActivityTracker activityTracker)
{
    public OpcUaConnectionState ConnectionState => connectionState;
    public AutoReadySubscription AutoReady => autoReady;
    public ExecutionActivityTracker ActivityTracker => activityTracker;
}

/// <summary>
/// Сервисы для разрешения ошибок.
/// </summary>
public sealed class ErrorResolutionServices(
    TagWaiter tagWaiter,
    OpcUaTagService plcService,
    IErrorService errorService,
    INotificationService notifications)
{
    public TagWaiter TagWaiter => tagWaiter;
    public OpcUaTagService PlcService => plcService;
    public IErrorService ErrorService => errorService;
    public INotificationService Notifications => notifications;
}