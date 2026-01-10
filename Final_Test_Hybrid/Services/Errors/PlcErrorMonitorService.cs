using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class PlcErrorMonitorService(
    OpcUaSubscription subscription,
    IErrorService errorService,
    ILogger<PlcErrorMonitorService> logger) : IPlcErrorMonitorService
{
    private (string? StepId, string? StepName) _currentStep;

    public async Task StartMonitoringAsync(CancellationToken ct = default)
    {
        foreach (var errorDef in ErrorDefinitions.PlcErrors)
        {
            await SubscribeToErrorTagAsync(errorDef, ct);
        }
    }

    public void SetCurrentStep(string? stepId, string? stepName)
        => _currentStep = (stepId, stepName);

    public void ClearCurrentStep()
        => _currentStep = (null, null);

    private async Task SubscribeToErrorTagAsync(ErrorDefinition errorDef, CancellationToken ct)
    {
        if (errorDef.PlcTag is null)
        {
            return;
        }

        try
        {
            await subscription.SubscribeAsync(errorDef.PlcTag, value => OnTagChanged(errorDef, value), ct);
            logger.LogDebug("Подписка на PLC ошибку {Code}: {Tag}", errorDef.Code, errorDef.PlcTag);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось подписаться на PLC ошибку {Code}: {Tag}", errorDef.Code, errorDef.PlcTag);
        }
    }

    private Task OnTagChanged(ErrorDefinition error, object? value)
    {
        if (value is not bool isActive)
        {
            return Task.CompletedTask;
        }
        var (stepId, stepName) = GetStepContextIfApplicable(error);
        if (isActive)
        {
            errorService.RaisePlc(error, stepId, stepName);
        }
        else
        {
            errorService.ClearPlc(error.Code);
        }
        return Task.CompletedTask;
    }

    private (string?, string?) GetStepContextIfApplicable(ErrorDefinition error)
    {
        return error.IsGlobal ? (null, null) : _currentStep;
    }
}
