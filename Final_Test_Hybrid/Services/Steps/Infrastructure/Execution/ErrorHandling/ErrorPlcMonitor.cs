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
        logger.LogInformation("Создание системных подписок: ErrorRetry, ErrorSkip, Fault, EndStep, AskEnd");

        await EnsureTagSubscribedAsync(BaseTags.ErrorRetry, ct);
        await EnsureTagSubscribedAsync(BaseTags.ErrorSkip, ct);
        await EnsureTagSubscribedAsync(BaseTags.Fault, ct);
        await EnsureTagSubscribedAsync(BaseTags.TestEndStep, ct);
        await EnsureTagSubscribedAsync(BaseTags.AskEnd, ct);

        logger.LogInformation("Физические системные подписки созданы");
    }

    private async Task EnsureTagSubscribedAsync(string nodeId, CancellationToken ct)
    {
        var error = await subscription.AddTagAsync(nodeId, ct);
        if (error != null)
        {
            throw new InvalidOperationException($"Не удалось подписаться на {nodeId}: {error.Message}");
        }
    }
}
