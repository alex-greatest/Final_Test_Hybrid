using Final_Test_Hybrid.Models.Plc.Subcription;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Plc;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Validation;

public class PreExecutionPlcValidator(
    OpcUaSubscription subscription,
    ILogger<PreExecutionPlcValidator> logger)
{
    public async Task<PlcSubscriptionValidationResult> ValidateAsync(
        IReadOnlyList<IPreExecutionStep> steps,
        CancellationToken ct = default)
    {
        var plcSteps = steps.OfType<IRequiresPlcTags>().ToList();
        if (plcSteps.Count == 0)
        {
            logger.LogInformation("Нет PreExecution шагов с PLC тегами");
            return new PlcSubscriptionValidationResult(true, []);
        }

        var requiredTags = plcSteps
            .SelectMany(s => s.RequiredPlcTags)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        logger.LogInformation("Валидация PreExecution PLC тегов: {Count}", requiredTags.Count);

        var errors = await subscription.AddTagsAsync(requiredTags, ct);
        var failedSubscriptions = MapErrors(plcSteps, errors);
        LogResult(failedSubscriptions);

        return new PlcSubscriptionValidationResult(failedSubscriptions.Count == 0, failedSubscriptions);
    }

    private static List<FailedSubscriptionInfo> MapErrors(
        List<IRequiresPlcTags> steps,
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
        IRequiresPlcTags step,
        Dictionary<string, string> errorDict)
    {
        var stepName = step is IPreExecutionStep preStep ? preStep.Name : "Unknown";
        foreach (var tag in step.RequiredPlcTags)
        {
            if (errorDict.TryGetValue(tag, out var message))
            {
                yield return new FailedSubscriptionInfo(stepName, tag, message);
            }
        }
    }

    private void LogResult(List<FailedSubscriptionInfo> failures)
    {
        if (failures.Count == 0)
        {
            logger.LogInformation("Все PreExecution PLC теги валидны");
            return;
        }
        logger.LogWarning("Ошибки PreExecution PLC тегов: {Count}", failures.Count);
        foreach (var failure in failures)
        {
            logger.LogWarning("Шаг '{StepName}' - тег '{NodeId}': {Error}",
                failure.StepName, failure.NodeId, failure.ErrorMessage);
        }
    }
}
