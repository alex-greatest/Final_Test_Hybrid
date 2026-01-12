using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Common.Logging;

/// <summary>
/// Комбинированный логгер, записывающий одновременно в ILogger и ITestStepLogger.
/// Устраняет дублирование вызовов logger.LogX + testStepLogger.LogX.
/// </summary>
public class DualLogger<TCategoryName>(
    ILogger<TCategoryName> logger,
    ITestStepLogger testStepLogger)
{
    public void LogDebug(string message, params object?[] args)
    {
        logger.LogDebug(message, args);
        testStepLogger.LogDebug(message, args);
    }

    public void LogInformation(string message, params object?[] args)
    {
        logger.LogInformation(message, args);
        testStepLogger.LogInformation(message, args);
    }

    public void LogWarning(string message, params object?[] args)
    {
        logger.LogWarning(message, args);
        testStepLogger.LogWarning(message, args);
    }

    public void LogError(string message, params object?[] args)
    {
        logger.LogError(message, args);
        testStepLogger.LogError(null, message, args);
    }

    public void LogError(Exception? ex, string message, params object?[] args)
    {
        logger.LogError(ex, message, args);
        testStepLogger.LogError(ex, message, args);
    }
}
