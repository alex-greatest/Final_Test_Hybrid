using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorHandling;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Validation;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.OpcUa;

public class PlcInitializationCoordinator(
    OpcUaConnectionState connectionState,
    ErrorPlcMonitor errorRecoveryMonitor,
    PlcSubscriptionInitializer subscriptionInitializer,
    PreExecutionPlcValidator preExecutionValidator,
    IPreExecutionStepRegistry preExecutionStepRegistry,
    IPlcErrorMonitorService plcErrorMonitor,
    PlcSubscriptionState subscriptionState,
    ITestStepRegistry stepRegistry,
    ILogger<PlcInitializationCoordinator> logger)
{
    public async Task InitializeAllAsync(CancellationToken ct = default)
    {
        await connectionState.WaitForConnectionAsync(ct);
        logger.LogInformation("Начало инициализации PLC");

        // 1. Реальные runtime-подписки (только тут показываем спиннер)
        await RunRuntimeSubscriptionsInitializationAsync(ct);

        // 2. Валидация тегов PreExecution шагов (IRequiresPlcTags)
        await ValidatePreExecutionTagsAsync(ct);

        // 3. Валидация связей RelatedStepId
        ValidateErrorStepBindings();

        logger.LogInformation("PLC инициализация завершена");
    }

    private async Task RunRuntimeSubscriptionsInitializationAsync(CancellationToken ct)
    {
        subscriptionState.SetInitializing();
        try
        {
            // 1. Валидация тегов обработки ошибок (ErrorRetry, ErrorSkip)
            await errorRecoveryMonitor.ValidateTagsAsync(ct);

            // 2. Подписка на теги шагов (IRequiresPlcSubscriptions)
            await subscriptionInitializer.InitializeAsync(ct);

            // 3. Подписка на теги ПЛК-ошибок
            await plcErrorMonitor.StartMonitoringAsync(ct);
        }
        finally
        {
            subscriptionState.SetCompleted();
        }
    }

    private async Task ValidatePreExecutionTagsAsync(CancellationToken ct)
    {
        var steps = preExecutionStepRegistry.GetOrderedSteps();
        var result = await preExecutionValidator.ValidateAsync(steps, ct);
        if (!result.IsValid)
        {
            throw new PlcSubscriptionException(result.FailedSubscriptions);
        }
    }

    private void ValidateErrorStepBindings()
    {
        var stepIds = stepRegistry.Steps.Select(s => s.Id)
            .Concat(preExecutionStepRegistry.GetOrderedSteps().Select(s => s.Id))
            .ToHashSet();

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
