using System.Globalization;
using Final_Test_Hybrid.Services.Common.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Final_Test_Hybrid.Services.DependencyInjection;

public static class LoggingServiceExtensions
{
    public static IServiceCollection AddLoggingServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        ConfigureSerilog(config);

        services.AddSingleton<ITestStepLogger, TestStepLogger>();
        services.AddTransient(typeof(DualLogger<>));

        var logLevel = Enum.Parse<LogLevel>(config["Logging:General:LogLevel"] ?? "Warning");

        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(logLevel);
#if DEBUG
            logging.AddDebug();
            logging.AddConsole();
#endif
            logging.AddSerilog(Log.Logger, dispose: true);
        });

        return services;
    }

    private static void ConfigureSerilog(IConfiguration config)
    {
        var logConfig = config.GetSection("Logging:General");
        var path = logConfig["Path"] ?? "D:/Logs/app-.txt";
        var retain = int.Parse(logConfig["RetainedFileCountLimit"] ?? "5", CultureInfo.InvariantCulture);
        var level = Enum.Parse<Serilog.Events.LogEventLevel>(logConfig["LogLevel"] ?? "Warning");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(path, rollingInterval: RollingInterval.Day, retainedFileCountLimit: retain)
            .CreateLogger();
    }
}
