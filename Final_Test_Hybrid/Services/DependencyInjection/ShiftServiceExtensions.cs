using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Settings.Spring.Shift;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class ShiftServiceExtensions
{
    public static IServiceCollection AddShiftServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<ShiftSettings>(config.GetSection("Shift"));
        services.AddSingleton<ShiftState>();
        services.AddSingleton<ShiftService>();

        return services;
    }
}
