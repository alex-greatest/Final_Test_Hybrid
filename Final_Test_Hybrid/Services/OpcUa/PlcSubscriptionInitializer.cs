using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionInitializer(
    PlcSubscriptionValidator validator,
    ITestStepRegistry stepRegistry,
    ILogger<PlcSubscriptionInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Начало подписки на теги шагов");

        var result = await validator.ValidateAsync(stepRegistry.Steps, ct);
        if (!result.IsValid)
        {
            throw new PlcSubscriptionException(result.FailedSubscriptions);
        }

        logger.LogInformation("Теги шагов подписаны");
    }
}
