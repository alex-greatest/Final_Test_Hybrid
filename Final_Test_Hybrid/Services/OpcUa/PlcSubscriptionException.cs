using System.Text;
using Final_Test_Hybrid.Services.Steps.Validation;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionException(IReadOnlyList<FailedSubscriptionInfo> failures)
    : Exception(BuildMessage(failures))
{
    public IReadOnlyList<FailedSubscriptionInfo> FailedSubscriptions { get; } = failures;

    private static string BuildMessage(IReadOnlyList<FailedSubscriptionInfo> failures)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Не удалось создать {failures.Count} PLC подписок:");
        foreach (var failure in failures)
        {
            sb.AppendLine($"  - {failure.StepName}: {failure.NodeId} ({failure.ErrorMessage})");
        }
        return sb.ToString();
    }
}
