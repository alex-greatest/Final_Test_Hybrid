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

        foreach (var errorDef in ErrorDefinitions.PlcErrors)
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
        if (!TryNormalizeBooleanValue(
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

    private static bool TryNormalizeBooleanValue(
        object? rawValue,
        out bool normalizedValue,
        out string normalizedType,
        out string normalizationNote)
    {
        normalizedValue = false;
        normalizedType = rawValue?.GetType().FullName ?? "null";
        normalizationNote = "UnsupportedType";

        switch (rawValue)
        {
            case bool boolValue:
                normalizedValue = boolValue;
                normalizedType = typeof(bool).FullName!;
                normalizationNote = "DirectBool";
                return true;

            case bool[] boolArray when boolArray.Length == 0:
                normalizationNote = "BoolArrayEmpty";
                return false;

            case bool[] boolArray:
                normalizedValue = boolArray[0];
                normalizedType = typeof(bool[]).FullName!;
                normalizationNote = boolArray.Length == 1
                    ? "BoolArrayLength1"
                    : $"BoolArrayLength{boolArray.Length}UseFirst";
                return true;

            case IEnumerable<bool> boolEnumerable:
                using (var enumerator = boolEnumerable.GetEnumerator())
                {
                    if (!enumerator.MoveNext())
                    {
                        normalizationNote = "BoolEnumerableEmpty";
                        return false;
                    }

                    normalizedValue = enumerator.Current;
                    var hasSecondValue = enumerator.MoveNext();
                    normalizedType = boolEnumerable.GetType().FullName
                        ?? "System.Collections.Generic.IEnumerable<System.Boolean>";
                    normalizationNote = hasSecondValue
                        ? "BoolEnumerableLength>1UseFirst"
                        : "BoolEnumerableLength1";
                    return true;
                }
        }

        return false;
    }
}
