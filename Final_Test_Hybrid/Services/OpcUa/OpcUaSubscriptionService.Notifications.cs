using Final_Test_Hybrid.Models.Plc;
using Microsoft.Extensions.Logging;
using Opc.Ua;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa;

public partial class OpcUaSubscriptionService
{
    private void OnMonitoredItemNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
    {
        var value = item.DequeueValues().LastOrDefault();
        if (value == null)
        {
            return;
        }
        var nodeId = item.StartNodeId.ToString();
        var opcValue = new OpcValue(value.Value, value.SourceTimestamp, StatusCode.IsGood(value.StatusCode));
        _logger.LogDebug(
            "Уведомление от {NodeId}: Value={Value}, Timestamp={Timestamp}, IsGood={IsGood}",
            nodeId, value.Value, value.SourceTimestamp, StatusCode.IsGood(value.StatusCode));
        _channel.Writer.TryWrite(new OpcValueChange(nodeId, opcValue));
    }

    private void StartProcessingTask()
    {
        _processingCts = new CancellationTokenSource();
        _processingTask = ProcessNotificationsAsync(_processingCts.Token);
    }

    private async Task ProcessNotificationsAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var change in _channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                ProcessSingleNotification(change);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Обработка уведомлений остановлена");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка в ProcessNotificationsAsync");
        }
    }

    private void ProcessSingleNotification(OpcValueChange change)
    {
        try
        {
            _cache[change.NodeId] = change.Value;
            NotifySubscribers(change.NodeId, change.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки уведомления для {NodeId}", change.NodeId);
        }
    }
}
