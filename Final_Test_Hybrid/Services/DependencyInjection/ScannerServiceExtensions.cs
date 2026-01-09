using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class ScannerServiceExtensions
{
    public static IServiceCollection AddScannerServices(this IServiceCollection services)
    {
        services.AddSingleton<ScannerConnectionState>();
        services.AddSingleton<RawInputService>();

        return services;
    }
}
