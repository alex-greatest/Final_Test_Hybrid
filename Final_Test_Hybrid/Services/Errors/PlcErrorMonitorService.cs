using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class PlcErrorMonitorService(
    OpcUaSubscription subscription,
    IErrorService errorService,
    ILogger<PlcErrorMonitorService> logger) : IPlcErrorMonitorService
{
    private int _isStarted;

    public async Task StartMonitoringAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _isStarted, 1) == 1)
        {
            return;
        }

        foreach (var errorDef in ErrorDefinitions.PlcErrors.Where(ShouldMonitorImmediately))
        {
            await SubscribeToErrorTagAsync(errorDef, ct);
        }
    }

    private async Task SubscribeToErrorTagAsync(ErrorDefinition errorDef, CancellationToken ct)
    {
        await subscription.SubscribeAsync(errorDef.PlcTag!, value => OnTagChanged(errorDef, value), ct);
        logger.LogDebug("Подписка на PLC ошибку {Code}: {Tag}", errorDef.Code, errorDef.PlcTag);
    }

    private Task OnTagChanged(ErrorDefinition error, object? value)
    {
        if (!PlcErrorValueNormalizer.TryNormalizeBooleanValue(
                value,
                out var isActive,
                out var normalizedType,
                out var normalizationNote))
        {
            logger.LogWarning(
                "PLC error callback skipped (не bool): Code={Code}, Tag={Tag}, Value={Value}, Type={Type}, NormalizeNote={NormalizeNote}",
                error.Code,
                error.PlcTag,
                value,
                normalizedType,
                normalizationNote);

            return Task.CompletedTask;
        }

        if (isActive)
        {
            errorService.RaisePlc(error, error.RelatedStepId, error.RelatedStepName);
        }
        else
        {
            errorService.ClearPlc(error.Code);
        }

        return Task.CompletedTask;
    }

    private static bool ShouldMonitorImmediately(ErrorDefinition error)
    {
        return !ErrorDefinitions.DeferredPlcErrors.Any(def => def.Code == error.Code);
    }
}
