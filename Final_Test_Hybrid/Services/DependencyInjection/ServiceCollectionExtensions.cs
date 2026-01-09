using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFinalTestServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        return services
            .AddLoggingServices(config)
            .AddCommonServices(config)
            .AddOpcUaServices(config)
            .AddSpringBootServices(config)
            .AddShiftServices(config)
            .AddScannerServices()
            .AddDatabaseServices(config)
            .AddDiagnosticServices(config)
            .AddStepsServices();
    }
}
