using Final_Test_Hybrid.Services.Common;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Services.OpcUa.Subscription;

public partial class OpcUaSubscription
{
    private const int RecreateRetryAttempts = 3;
    private static readonly TimeSpan RecreateRetryDelay = TimeSpan.FromMilliseconds(300);

    public async Task RecreateForSessionAsync(ISession session, CancellationToken ct = default)
    {
        await using (await AsyncLock.AcquireAsync(_subscriptionLock, ct).ConfigureAwait(false))
        {
            var trackedNodeIds = SnapshotTrackedNodeIds();
            await DisposeCurrentSubscriptionAsync(ct).ConfigureAwait(false);
            ResetMonitoredItemsForRecreate();
            InvalidateValuesCache();
            _subscription = await CreateSubscriptionWithItemsRetryAsync(session, trackedNodeIds, ct).ConfigureAwait(false);
            logger.LogInformation(
                "OPC UA подписка пересоздана. Восстановлено monitored items: {Count}",
                trackedNodeIds.Count);
        }
    }

    private IReadOnlyList<string> SnapshotTrackedNodeIds()
    {
        var nodeIds = new HashSet<string>(_monitoredItems.Keys);
        lock (_callbacksLock)
        {
            foreach (var nodeId in _callbacks.Keys)
            {
                nodeIds.Add(nodeId);
            }
        }

        return [.. nodeIds];
    }

    private async Task DisposeCurrentSubscriptionAsync(CancellationToken ct)
    {
        if (_subscription == null)
        {
            return;
        }

        var currentSubscription = _subscription;
        _subscription = null;

        try
        {
            var currentSession = currentSubscription.Session;
            if (currentSession != null)
            {
                await currentSession.RemoveSubscriptionAsync(currentSubscription, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                "Не удалось снять старую OPC UA подписку при пересоздании: {Error}",
                ex.Message);
        }
        finally
        {
            currentSubscription.Dispose();
        }
    }

    private void ResetMonitoredItemsForRecreate()
    {
        foreach (var item in _monitoredItems.Values)
        {
            item.Notification -= OnNotification;
        }

        _monitoredItems.Clear();
    }

    private async Task<Opc.Ua.Client.Subscription> CreateSubscriptionWithItemsRetryAsync(
        ISession session,
        IReadOnlyList<string> nodeIds,
        CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var newSubscription = CreateSubscription(session);
            try
            {
                session.AddSubscription(newSubscription);
                await newSubscription.CreateAsync(ct).ConfigureAwait(false);
                FillMonitoredItemsForRecreate(newSubscription, nodeIds);
                await newSubscription.ApplyChangesAsync(ct).ConfigureAwait(false);
                return newSubscription;
            }
            catch (Exception ex) when (ShouldRetryRecreate(ex, attempt))
            {
                await CleanupFailedSubscriptionAsync(session, newSubscription).ConfigureAwait(false);
                ResetMonitoredItemsForRecreate();
                await DelayRecreateRetryAsync(ex, attempt, ct).ConfigureAwait(false);
            }
            catch
            {
                await CleanupFailedSubscriptionAsync(session, newSubscription).ConfigureAwait(false);
                ResetMonitoredItemsForRecreate();
                throw;
            }
        }
    }

    private void FillMonitoredItemsForRecreate(
        Opc.Ua.Client.Subscription targetSubscription,
        IReadOnlyList<string> nodeIds)
    {
        _subscription = targetSubscription;
        foreach (var nodeId in nodeIds)
        {
            var item = CreateMonitoredItem(nodeId);
            _monitoredItems[nodeId] = item;
            targetSubscription.AddItem(item);
        }
    }

    private async Task CleanupFailedSubscriptionAsync(
        ISession session,
        Opc.Ua.Client.Subscription subscriptionToCleanup)
    {
        try
        {
            await session.RemoveSubscriptionAsync(subscriptionToCleanup, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                "Не удалось снять частично созданную подписку после ошибки пересоздания: {Error}",
                ex.Message);
        }
        finally
        {
            subscriptionToCleanup.Dispose();
            _subscription = null;
        }
    }

    private static bool ShouldRetryRecreate(Exception ex, int attempt)
    {
        return attempt < RecreateRetryAttempts
            && OpcUaTransientErrorClassifier.IsTransientDisconnect(ex);
    }

    private async Task DelayRecreateRetryAsync(Exception ex, int attempt, CancellationToken ct)
    {
        logger.LogWarning(
            "Не удалось пересоздать OPC UA подписку. Попытка {Attempt}/{MaxAttempts}. Ошибка: {Error}",
            attempt,
            RecreateRetryAttempts,
            ex.Message);
        await Task.Delay(RecreateRetryDelay, ct).ConfigureAwait(false);
    }
}
