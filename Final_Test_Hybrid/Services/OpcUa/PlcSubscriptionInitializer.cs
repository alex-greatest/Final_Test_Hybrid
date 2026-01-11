using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcSubscriptionInitializer(
    OpcUaConnectionState connectionState,
    PlcSubscriptionValidator validator,
    PlcSubscriptionState subscriptionState,
    ITestStepRegistry stepRegistry,
    ILogger<PlcSubscriptionInitializer> logger)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await connectionState.WaitForConnectionAsync(ct);
        logger.LogInformation("Начало инициализации PLC подписок");
        var result = await validator.ValidateAsync(stepRegistry.Steps, ct);
        if (!result.IsValid)
        {
            throw new PlcSubscriptionException(result.FailedSubscriptions);
        }
        logger.LogInformation("PLC подписки успешно инициализированы");
        subscriptionState.SetCompleted();
    }
}
