using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcInitializationCoordinator(
    OpcUaConnectionState connectionState,
    ErrorPlcMonitor errorRecoveryMonitor,
    PlcSubscriptionInitializer subscriptionInitializer,
    IPlcErrorMonitorService plcErrorMonitor,
    PlcSubscriptionState subscriptionState,
    ITestStepRegistry stepRegistry,
    ILogger<PlcInitializationCoordinator> logger)
{
    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        await connectionState.WaitForConnectionAsync(ct);

        logger.LogInformation("Начало инициализации PLC");

        // 1. Валидация тегов обработки ошибок (ErrorRetry, ErrorSkip)
        await errorRecoveryMonitor.ValidateTagsAsync(ct);

        // 2. Подписка на теги шагов (IRequiresPlcSubscriptions)
        await subscriptionInitializer.InitializeAsync(ct);

        // 3. Подписка на теги ПЛК-ошибок
        await plcErrorMonitor.StartMonitoringAsync(ct);

        // 4. Валидация связей RelatedStepId
        ValidateErrorStepBindings();

        logger.LogInformation("PLC инициализация завершена");
        subscriptionState.SetCompleted();
    }

    private void ValidateErrorStepBindings()
    {
        var stepIds = stepRegistry.Steps.Select(s => s.Id).ToHashSet();
        var invalidErrors = ErrorDefinitions.All
            .Where(e => e.RelatedStepId != null && !stepIds.Contains(e.RelatedStepId))
            .ToList();
        if (invalidErrors.Count > 0)
        {
            var details = string.Join(", ", invalidErrors.Select(e => $"{e.Code}→{e.RelatedStepId}"));
            throw new InvalidOperationException($"Invalid RelatedStepId in errors: {details}");
        }
    }
}
