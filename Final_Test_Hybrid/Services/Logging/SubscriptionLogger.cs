using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Final_Test_Hybrid.Services.Logging;

[SuppressMessage("Serilog", "Serilog004:Constant MessageTemplate verifier", Justification = "Template is verified at call site via MessageTemplateFormatMethod attribute")]
public class SubscriptionLogger : ISubscriptionLogger, IDisposable
{
    private readonly ILogger _logger;
    private bool _disposed;

    public SubscriptionLogger(IConfiguration config)
    {
        var logConfig = config.GetSection("Logging:Subscription");
        var path = logConfig["Path"] ?? "D:/Logs/Subscriptions/subscription-.txt";
        var retain = int.Parse(logConfig["RetainedFileCountLimit"] ?? "10", CultureInfo.InvariantCulture);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(path, rollingInterval: RollingInterval.Infinite, retainedFileCountLimit: retain)
            .CreateLogger();
    }

    public void LogDebug(string message, params object?[] args) => _logger.Debug(message, args);
    public void LogInformation(string message, params object?[] args) => _logger.Information(message, args);
    public void LogWarning(string message, params object?[] args) => _logger.Warning(message, args);
    public void LogError(Exception? ex, string message, params object?[] args) => _logger.Error(ex, message, args);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        (_logger as IDisposable)?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
