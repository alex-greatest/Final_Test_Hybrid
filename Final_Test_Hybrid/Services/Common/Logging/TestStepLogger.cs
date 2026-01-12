using System.Globalization;
using Microsoft.Extensions.Configuration;
using Serilog;
using ILogger = Serilog.ILogger;

namespace Final_Test_Hybrid.Services.Common.Logging;

public class TestStepLogger : ITestStepLogger, IDisposable
{
    private readonly string _basePath;
    private readonly int _retain;
    private readonly Serilog.Events.LogEventLevel _level;
    private ILogger? _logger;
    private string? _currentLogPath;
    private bool _disposed;
    public void LogDebug(string message, params object?[] args) => _logger?.Debug(message, args);
    public void LogInformation(string message, params object?[] args) => _logger?.Information(message, args);
    public void LogWarning(string message, params object?[] args) => _logger?.Warning(message, args);
    public void LogError(Exception? ex, string message, params object?[] args) => _logger?.Error(ex, message, args);

    public TestStepLogger(IConfiguration config)
    {
        var logConfig = config.GetSection("Logging:TestStep");
        _basePath = logConfig["Path"] ?? "D:/Logs/TestSteps/teststep-.txt";
        _retain = int.Parse(logConfig["RetainedFileCountLimit"] ?? "10", CultureInfo.InvariantCulture);
        _level = Enum.Parse<Serilog.Events.LogEventLevel>(logConfig["LogLevel"] ?? "Debug");
    }

    public void StartNewSession()
    {
        (_logger as IDisposable)?.Dispose();
        var path = BuildPathWithTimestamp(_basePath);
        _currentLogPath = path;
        CleanupOldFiles(_basePath, _retain);
        _logger = new LoggerConfiguration()
            .MinimumLevel.Is(_level)
            .WriteTo.File(path)
            .CreateLogger();
    }

    public string? GetCurrentLogFilePath() => _currentLogPath;

    public void LogStepStart(string stepName)
    {
        _logger?.Information("[Начало шага] {StepName}", stepName);
    }

    public void LogStepEnd(string stepName)
    {
        _logger?.Information("[Конец шага] {StepName}", stepName);
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
