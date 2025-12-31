using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interaces.Test;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public record FailedSubscriptionInfo(string StepName, string NodeId, string ErrorMessage);

public record PlcSubscriptionValidationResult(
    bool IsValid,
    IReadOnlyList<FailedSubscriptionInfo> FailedSubscriptions);

public class PlcSubscriptionValidator(
    OpcUaSubscription subscription,
    ILogger<PlcSubscriptionValidator> logger)
{
    public async Task<PlcSubscriptionValidationResult> ValidateAsync(
        IReadOnlyList<ITestStep> steps,
        CancellationToken ct = default)
    {
        var plcSteps = CollectPlcSteps(steps);
        if (plcSteps.Count == 0)
        {
            logger.LogInformation("Нет шагов с PLC подписками");
            return new PlcSubscriptionValidationResult(true, []);
        }

        var requiredTags = CollectRequiredTags(plcSteps);
        logger.LogInformation("Инициализация PLC подписок: {Count} тегов", requiredTags.Count);

        var errors = await subscription.AddTagsAsync(requiredTags, ct);
        var failedSubscriptions = MapErrors(plcSteps, errors);
        LogResult(failedSubscriptions);

        return new PlcSubscriptionValidationResult(failedSubscriptions.Count == 0, failedSubscriptions);
    }

    private static List<IRequiresPlcSubscriptions> CollectPlcSteps(IReadOnlyList<ITestStep> steps)
    {
        return steps.OfType<IRequiresPlcSubscriptions>().ToList();
    }

    private static HashSet<string> CollectRequiredTags(List<IRequiresPlcSubscriptions> steps)
    {
        return steps
            .SelectMany(s => s.RequiredPlcTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static List<FailedSubscriptionInfo> MapErrors(
        List<IRequiresPlcSubscriptions> steps,
        IReadOnlyList<TagError> errors)
    {
        if (errors.Count == 0)
        {
            return [];
        }

        var errorDict = errors.ToDictionary(e => e.NodeId, e => e.Message, StringComparer.OrdinalIgnoreCase);
        return steps
            .SelectMany(step => MapStepErrors(step, errorDict))
            .ToList();
    }

    private static IEnumerable<FailedSubscriptionInfo> MapStepErrors(
        IRequiresPlcSubscriptions step,
        Dictionary<string, string> errorDict)
    {
        foreach (var tag in step.RequiredPlcTags)
        {
            if (errorDict.TryGetValue(tag, out var message))
            {
                yield return new FailedSubscriptionInfo(step.Name, tag, message);
            }
        }
    }

    private void LogResult(List<FailedSubscriptionInfo> failures)
    {
        if (failures.Count == 0)
        {
            logger.LogInformation("Все PLC подписки успешно созданы");
            return;
        }

        logger.LogWarning("Ошибки PLC подписок: {Count}", failures.Count);
        foreach (var failure in failures)
        {
            logger.LogWarning("Шаг '{StepName}' - тег '{NodeId}': {Error}",
                failure.StepName, failure.NodeId, failure.ErrorMessage);
        }
    }
}
