using Final_Test_Hybrid.Services.SpringBoot.ErrorSettings;
using Final_Test_Hybrid.Services.SpringBoot.Health;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.SpringBoot.ResultSettings;
using Final_Test_Hybrid.Services.SpringBoot.StepFinalTest;
using Final_Test_Hybrid.Settings.Spring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class SpringBootServiceExtensions
{
    public static IServiceCollection AddSpringBootServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<SpringBootSettings>(config.GetSection("SpringBoot"));
        services.AddSingleton<SpringBootConnectionState>();
        services.AddSingleton<SpringBootHttpClient>();
        services.AddSingleton<SpringBootHealthService>();
        services.AddSingleton<OperatorState>();
        services.AddSingleton<OperatorAuthService>();
        services.AddScoped<RecipeDownloadService>();
        services.AddScoped<ResultSettingsDownloadService>();
        services.AddScoped<ErrorSettingsDownloadService>();
        services.AddScoped<StepFinalTestDownloadService>();
        services.AddScoped<OperationStartService>();
        services.AddScoped<ReworkDialogService>();

        return services;
    }
}
