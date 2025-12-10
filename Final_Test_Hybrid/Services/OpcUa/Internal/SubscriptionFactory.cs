using Final_Test_Hybrid.Models.Plc;
using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Internal;

internal sealed class SubscriptionFactory(OpcUaSettings settings, ILogger<SubscriptionFactory> logger)
{
    private Subscription? _subscription;
    private Action<string, OpcValue>? _onNotification;

    public async Task<SubscriptionResult> CreateAsync(
        ISession session,
        IEnumerable<string> nodeIds,
        Action<string, OpcValue> onNotification,
        CancellationToken ct)
    {
        if (!IsSessionAvailable(session))
        {
            logger.LogWarning("Невозможно создать подписку: сессия недоступна");
            return SubscriptionResult.Empty;
        }
        _onNotification = onNotification;
        return await CreateSubscriptionAsync(session, nodeIds.ToList(), ct).ConfigureAwait(false);
    }

    public async Task RemoveAsync(ISession? session)
    {
        if (_subscription == null)
        {
            return;
        }
        try
        {
            UnsubscribeFromNotifications();
            await DeleteFromServerAsync(session).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка при удалении подписки");
        }
        finally
        {
            DisposeSubscription();
        }
    }

    private async Task<SubscriptionResult> CreateSubscriptionAsync(
        ISession session,
        List<string> nodeIds,
        CancellationToken ct)
    {
        var subscriptionSettings = settings.Subscription;
        _subscription = new Subscription(session.DefaultSubscription)
        {
            PublishingEnabled = true,
            PublishingInterval = subscriptionSettings.PublishingIntervalMs,
            KeepAliveCount = 10,
            LifetimeCount = 100,
            MaxNotificationsPerPublish = 0
        };
        session.AddSubscription(_subscription);
        await _subscription.CreateAsync(ct).ConfigureAwait(false);
        logger.LogInformation(
            "OPC UA Subscription создана: PublishingInterval={Interval}ms",
            subscriptionSettings.PublishingIntervalMs);
        return await AddMonitoredItemsAsync(nodeIds, subscriptionSettings, ct).ConfigureAwait(false);
    }

    private async Task<SubscriptionResult> AddMonitoredItemsAsync(
        List<string> nodeIds,
        OpcUaSubscriptionSettings subscriptionSettings,
        CancellationToken ct)
    {
        var items = nodeIds.Select(id => CreateMonitoredItem(id, subscriptionSettings)).ToList();
        _subscription!.AddItems(items);
        await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
        return BuildResult(items);
    }

    private MonitoredItem CreateMonitoredItem(string nodeId, OpcUaSubscriptionSettings subscriptionSettings)
    {
        var item = new MonitoredItem(_subscription!.DefaultItem)
        {
            StartNodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            MonitoringMode = MonitoringMode.Reporting,
            SamplingInterval = subscriptionSettings.SamplingIntervalMs,
            QueueSize = (uint)subscriptionSettings.QueueSize,
            DiscardOldest = true
        };
        item.Notification += OnMonitoredItemNotification;
        return item;
    }

    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        var value = item.DequeueValues().LastOrDefault();
        if (value == null)
        {
            return;
        }
        var nodeId = item.StartNodeId.ToString();
        var opcValue = new OpcValue(value.Value, value.SourceTimestamp, StatusCode.IsGood(value.StatusCode));
        logger.LogDebug(
            "Уведомление от {NodeId}: Value={Value}, Timestamp={Timestamp}, IsGood={IsGood}",
            nodeId, value.Value, value.SourceTimestamp, StatusCode.IsGood(value.StatusCode));
        _onNotification?.Invoke(nodeId, opcValue);
    }

    private SubscriptionResult BuildResult(List<MonitoredItem> items)
    {
        var successful = new List<string>();
        var failed = new List<SubscriptionError>();
        foreach (var item in items)
        {
            ClassifyItem(item, successful, failed);
        }
        LogResults(successful, failed);
        return new SubscriptionResult(successful, failed);
    }

    private void ClassifyItem(MonitoredItem item, List<string> successful, List<SubscriptionError> failed)
    {
        if (ServiceResult.IsGood(item.Status.Error))
        {
            successful.Add(item.StartNodeId.ToString());
            return;
        }
        failed.Add(new SubscriptionError(
            item.StartNodeId.ToString(),
            GetHumanReadableError(item.Status.Error)));
    }

    private static string GetHumanReadableError(ServiceResult error)
    {
        return error.Code switch
        {
            StatusCodes.BadNodeIdUnknown => "Тег не найден на сервере",
            StatusCodes.BadNodeIdInvalid => "Неверный формат тега",
            StatusCodes.BadUserAccessDenied => "Нет доступа к тегу",
            StatusCodes.BadAttributeIdInvalid => "Неверный атрибут тега",
            StatusCodes.BadNotReadable => "Тег недоступен для чтения",
            StatusCodes.BadMonitoringModeInvalid => "Тег не поддерживает мониторинг",
            StatusCodes.BadOutOfRange => "Значение вне допустимого диапазона",
            StatusCodes.BadTypeMismatch => "Несоответствие типа данных",
            _ => $"Неизвестная ошибка: {error.LocalizedText}"
        };
    }

    private void LogResults(List<string> successful, List<SubscriptionError> failed)
    {
        logger.LogInformation(
            "Подписки созданы: {Success} успешно, {Failed} с ошибками",
            successful.Count, failed.Count);
    }

    private void UnsubscribeFromNotifications()
    {
        foreach (var item in _subscription!.MonitoredItems)
        {
            item.Notification -= OnMonitoredItemNotification;
        }
    }

    private async Task DeleteFromServerAsync(ISession? session)
    {
        if (!IsSessionAvailable(session))
        {
            return;
        }
        await session!.RemoveSubscriptionAsync(_subscription!).ConfigureAwait(false);
    }

    private void DisposeSubscription()
    {
        _subscription?.Dispose();
        _subscription = null;
    }

    private static bool IsSessionAvailable(ISession? session)
    {
        return session is { Connected: true };
    }
}
