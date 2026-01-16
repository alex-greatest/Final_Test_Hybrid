using Final_Test_Hybrid.Services.Common.IO;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Common.UI;
using Final_Test_Hybrid.Services.Editor;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class CommonServiceExtensions
{
    public static IServiceCollection AddCommonServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        services.Configure<AppSettings>(config.GetSection("Settings"));
        services.AddSingleton<AppSettingsService>();

        // UI services
        services.AddSingleton<BlazorDispatcherAccessor>();
        services.AddSingleton<IUiDispatcher, BlazorUiDispatcher>();
        services.AddSingleton<INotificationService, NotificationServiceWrapper>();

        // File services
        services.AddScoped<IFilePickerService, WinFormsFilePickerService>();
        services.AddScoped<ISequenceExcelService, SequenceExcelService>();

        // Test sequence
        services.AddScoped<TestSequenceService>();

        return services;
    }
}
