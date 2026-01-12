namespace Final_Test_Hybrid.Services.Common.Logging;

public interface ITestStepLogger
{
    void StartNewSession();
    void LogStepStart(string stepName);
    void LogStepEnd(string stepName);
    void LogDebug(string message, params object?[] args);
    void LogInformation(string message, params object?[] args);
    void LogWarning(string message, params object?[] args);
    void LogError(Exception? ex, string message, params object?[] args);
    string? GetCurrentLogFilePath();
}
