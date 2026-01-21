using Final_Test_Hybrid.Services.Database;
using Final_Test_Hybrid.Services.Database.Config;
using Final_Test_Hybrid.Settings.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class DatabaseServiceExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        var dbSettings = config.GetSection("Database").Get<DatabaseSettings>();
        services.Configure<DatabaseSettings>(config.GetSection("Database"));

        services.AddDbContextFactory<AppDbContext>(options =>
        {
            options.UseNpgsql(dbSettings?.ConnectionString ?? string.Empty, npgsqlOptions =>
            {
                npgsqlOptions.CommandTimeout(dbSettings?.ConnectionTimeoutSeconds ?? 5);
            });
        });

        services.AddSingleton<DatabaseConnectionState>();
        services.AddSingleton<DatabaseConnectionService>();
        services.AddSingleton<SuccessCountService>();
        services.AddScoped<BoilerTypeService>();
        services.AddScoped<BoilerService>();
        services.AddScoped<OperationService>();
        services.AddScoped<RecipeService>();
        services.AddScoped<ResultSettingsService>();
        services.AddScoped<StepFinalTestService>();
        services.AddScoped<ErrorSettingsTemplateService>();

        return services;
    }
}
