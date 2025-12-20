using Serilog.Core;

namespace Final_Test_Hybrid.Services.Common.Logging;

public interface ISpringBootLogger
{
    [MessageTemplateFormatMethod("message")]
    void LogDebug(string message, params object?[] args);
    [MessageTemplateFormatMethod("message")]
    void LogInformation(string message, params object?[] args);
    [MessageTemplateFormatMethod("message")]
    void LogWarning(string message, params object?[] args);
    [MessageTemplateFormatMethod("message")]
    void LogError(Exception? ex, string message, params object?[] args);
}
