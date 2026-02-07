using Final_Test_Hybrid.Services.Diagnostic.Access;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Polling;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class DiagnosticServiceExtensions
{
    public static IServiceCollection AddDiagnosticServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<DiagnosticSettings>(config.GetSection("Diagnostic"));
        services.Configure<ModbusDispatcherOptions>(config.GetSection("Diagnostic:CommandQueue"));
        services.AddSingleton<DiagnosticConnectionState>();

        // Command Queue Architecture
        services.AddSingleton<ModbusConnectionManager>();
        services.AddSingleton<ModbusDispatcher>();
        services.AddSingleton<IModbusDispatcher>(sp => sp.GetRequiredService<ModbusDispatcher>());
        services.AddSingleton<QueuedModbusClient>();
        services.AddSingleton<IModbusClient>(sp => sp.GetRequiredService<QueuedModbusClient>());

        services.AddSingleton<RegisterReader>();
        services.AddSingleton<RegisterWriter>();
        services.AddSingleton<AccessLevelManager>();
        services.AddSingleton<PollingService>();

        // High-level boiler services
        services.AddSingleton<BoilerStatusService>();
        services.AddSingleton<BoilerTemperatureService>();
        services.AddSingleton<BoilerSensorsService>();
        services.AddSingleton<BoilerCountersService>();
        services.AddSingleton<BoilerDeviceInfoService>();
        services.AddSingleton<BoilerSettingsService>();

        // ECU error sync
        services.AddSingleton<EcuErrorSyncService>();
        services.AddSingleton<BoilerLockRuntimeService>();

        return services;
    }
}
