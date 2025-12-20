using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace Final_Test_Hybrid.Services.Common.Logging;

public class SpringBootLogger : ISpringBootLogger, IDisposable
{
    private readonly ILogger _logger;
    private bool _disposed;
    public void LogDebug(string message, params object?[] args) => _logger.Debug(message, args);
    public void LogInformation(string message, params object?[] args) => _logger.Information(message, args);
    public void LogWarning(string message, params object?[] args) => _logger.Warning(message, args);
    public void LogError(Exception? ex, string message, params object?[] args) => _logger.Error(ex, message, args);

    public SpringBootLogger(IConfiguration config)
    {
        var logConfig = config.GetSection("Logging:SpringBoot");
        var basePath = logConfig["Path"] ?? "D:/Logs/SpringBoot/springboot-.txt";
        var retain = int.Parse(logConfig["RetainedFileCountLimit"] ?? "10", CultureInfo.InvariantCulture);
        var level = Enum.Parse<Serilog.Events.LogEventLevel>(logConfig["LogLevel"] ?? "Debug");
        var path = BuildPathWithTimestamp(basePath);
        CleanupOldFiles(basePath, retain);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(level)
            .WriteTo.File(path)
            .CreateLogger();
    }

    private static string BuildPathWithTimestamp(string basePath)
    {
        var dir = Path.GetDirectoryName(basePath) ?? "";
        var name = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);
        var timestamp = DateTime.Now.ToString("dd.MM.yy_THH_mm_ss", CultureInfo.InvariantCulture);
        return Path.Combine(dir, $"{name}{timestamp}{ext}");
    }

    private static void CleanupOldFiles(string basePath, int retain)
    {
        var dir = Path.GetDirectoryName(basePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return;
        }
        var name = Path.GetFileNameWithoutExtension(basePath);
        var ext = Path.GetExtension(basePath);
        var files = Directory.GetFiles(dir, $"{name}*{ext}")
            .OrderByDescending(File.GetCreationTime)
            .Skip(retain - 1)
            .ToList();
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }

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
