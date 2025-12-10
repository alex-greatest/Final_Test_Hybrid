using Final_Test_Hybrid.Models.Plc;
using Final_Test_Hybrid.Models.Plc.Settings;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class OpcUaSubscriptionService
{
    private async Task<SubscriptionResult> CreateSubscriptionAsync(ISession session, List<string> nodeIds, CancellationToken ct)
    {
        if (!IsSessionAvailable(session))
        {
            _logger.LogWarning("Невозможно создать подписку: сессия недоступна");
            return new SubscriptionResult([], []);
        }
        return await CreateSubscribe(session, nodeIds, ct).ConfigureAwait(false);
    }

    private async Task<SubscriptionResult> CreateSubscribe(ISession session, List<string> nodeIds, CancellationToken ct)
    {
        var subscriptionSettings = _settings.Subscription;
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
        _logger.LogInformation(
            "OPC UA Subscription создана: PublishingInterval={Interval}ms",
            subscriptionSettings.PublishingIntervalMs);
        return await AddMonitoredItemsAsync(nodeIds, subscriptionSettings, ct).ConfigureAwait(false);
    }

    private async Task<SubscriptionResult> AddMonitoredItemsAsync(
        List<string> nodeIds,
        OpcUaSubscriptionSettings settings,
        CancellationToken ct)
    {
        var monitoredItems = CreateMonitoredItems(nodeIds, settings);
        await ApplyMonitoredItemsAsync(monitoredItems, ct).ConfigureAwait(false);
        return BuildSubscriptionResult(monitoredItems);
    }

    private List<MonitoredItem> CreateMonitoredItems(List<string> nodeIds, OpcUaSubscriptionSettings settings)
    {
        return nodeIds.Select(nodeId => CreateMonitoredItem(nodeId, settings)).ToList();
    }

    private MonitoredItem CreateMonitoredItem(string nodeId, OpcUaSubscriptionSettings settings)
    {
        var item = new MonitoredItem(_subscription!.DefaultItem)
        {
            StartNodeId = new NodeId(nodeId),
            AttributeId = Attributes.Value,
            MonitoringMode = MonitoringMode.Reporting,
            SamplingInterval = settings.SamplingIntervalMs,
            QueueSize = (uint)settings.QueueSize,
            DiscardOldest = true
        };
        item.Notification += OnMonitoredItemNotification;
        return item;
    }

    private async Task ApplyMonitoredItemsAsync(List<MonitoredItem> items, CancellationToken ct)
    {
        _subscription!.AddItems(items);
        await _subscription.ApplyChangesAsync(ct).ConfigureAwait(false);
    }

    private SubscriptionResult BuildSubscriptionResult(List<MonitoredItem> items)
    {
        var successful = new List<string>();
        var failed = new List<SubscriptionError>();

        foreach (var item in items)
        {
            ClassifyMonitoredItem(item, successful, failed);
        }

        LogSubscriptionResults(successful, failed);
        return new SubscriptionResult(successful, failed);
    }

    private void ClassifyMonitoredItem(MonitoredItem item, List<string> successful, List<SubscriptionError> failed)
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

    private string GetHumanReadableError(ServiceResult error)
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

    private void LogSubscriptionResults(List<string> successful, List<SubscriptionError> failed)
    {
        _logger.LogInformation("Подписки созданы: {Success} успешно, {Failed} с ошибками",
            successful.Count, failed.Count);
    }

    private async Task RemoveSubscriptionAsync()
    {
        if (_subscription == null)
        {
            return;
        }
        try
        {
            UnsubscribeFromNotifications();
            await DeleteFromServerAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении подписки");
        }
        finally
        {
            DisposeSubscription();
        }
    }

    private void UnsubscribeFromNotifications()
    {
        foreach (var item in _subscription!.MonitoredItems)
        {
            item.Notification -= OnMonitoredItemNotification;
        }
    }

    private async Task DeleteFromServerAsync()
    {
        if (!IsSessionAvailable(_connectionService.Session))
        {
            return;
        }
        await _connectionService.Session!.RemoveSubscriptionAsync(_subscription!).ConfigureAwait(false);
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
