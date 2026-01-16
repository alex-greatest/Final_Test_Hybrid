using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;

public class ErrorPlcMonitor(
    OpcUaSubscription subscription,
    ILogger<ErrorPlcMonitor> logger)
{
    public async Task ValidateTagsAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Создание подписок на теги ErrorRetry, ErrorSkip");

        var retryError = await subscription.AddTagAsync(BaseTags.ErrorRetry, ct);
        if (retryError != null)
        {
            throw new InvalidOperationException(
                $"Не удалось подписаться на {BaseTags.ErrorRetry}: {retryError.Message}");
        }

        var skipError = await subscription.AddTagAsync(BaseTags.ErrorSkip, ct);
        if (skipError != null)
        {
            throw new InvalidOperationException(
                $"Не удалось подписаться на {BaseTags.ErrorSkip}: {skipError.Message}");
        }

        var faultError = await subscription.AddTagAsync(BaseTags.Fault, ct);
        if (faultError != null)
        {
            throw new InvalidOperationException(
                $"Не удалось подписаться на {BaseTags.Fault}: {faultError.Message}");
        }

        var testEndStepError = await subscription.AddTagAsync(BaseTags.TestEndStep, ct);
        if (testEndStepError != null)
        {
            throw new InvalidOperationException(
                $"Не удалось подписаться на {BaseTags.TestEndStep}: {testEndStepError.Message}");
        }

        logger.LogInformation("Физические подписки на теги ошибок созданы");
    }
}
