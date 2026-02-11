using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Auto;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Heartbeat;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Settings.OpcUa;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class OpcUaServiceExtensions
{
    public static IServiceCollection AddOpcUaServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<OpcUaSettings>(config.GetSection("OpcUa"));
        services.AddSingleton<OpcUaConnectionState>();
        services.AddSingleton<OpcUaSubscription>();
        services.AddSingleton<OpcUaSubscriptionDiagnosticsService>();
        services.AddSingleton<OpcUaConnectionService>();
        services.AddSingleton<OpcUaTagService>();
        services.AddSingleton<OpcUaBrowseService>();
        services.AddSingleton<PlcAutoWriterService>();
        services.AddSingleton<PausableOpcUaTagService>();
        services.AddSingleton<TagWaiter>();
        services.AddSingleton<PausableTagWaiter>();

        // HMI Heartbeat - периодическое взведение флага для контроля связи с PLC
        // Сервис автоматически подписывается на ConnectionStateChanged при создании
        services.AddSingleton<HmiHeartbeatService>();

        return services;
    }
}
