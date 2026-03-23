namespace Final_Test_Hybrid.Services.OpcUa.Heartbeat;

internal enum HeartbeatHealthState
{
    Healthy,
    WriteFailed,
    MissedWindow
}

internal readonly record struct HmiHeartbeatHealthSnapshot(
    HeartbeatHealthState State,
    long? AgeMs,
    string LastWriteResult);

internal readonly record struct HmiHeartbeatHealthTransition(
    HeartbeatHealthState PreviousState,
    HeartbeatHealthState CurrentState,
    long? AgeMs,
    string LastWriteResult);

public sealed class HmiHeartbeatHealthMonitor(TimeProvider timeProvider)
{
    private static readonly TimeSpan MissedWindowThreshold = TimeSpan.FromSeconds(6);

    private readonly Lock _lock = new();
    private DateTimeOffset? _monitoringStartedUtc;
    private DateTimeOffset? _lastHeartbeatSuccessUtc;
    private HeartbeatHealthState _state = HeartbeatHealthState.Healthy;
    private string _lastWriteResult = "Ожидание первой записи";

    internal void MarkMonitoringStarted()
    {
        lock (_lock)
        {
            _monitoringStartedUtc = timeProvider.GetUtcNow();
            _lastHeartbeatSuccessUtc = null;
            _state = HeartbeatHealthState.Healthy;
            _lastWriteResult = "Ожидание первой записи";
        }
    }

    internal void MarkMonitoringStopped()
    {
        lock (_lock)
        {
            _monitoringStartedUtc = null;
        }
    }

    internal HmiHeartbeatHealthTransition? RecordWriteSuccess()
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            var previousState = GetEffectiveStateUnsafe(now);

            _lastHeartbeatSuccessUtc = now;
            _state = HeartbeatHealthState.Healthy;
            _lastWriteResult = "Успешно";

            return CreateTransitionUnsafe(previousState, HeartbeatHealthState.Healthy, now);
        }
    }

    internal HmiHeartbeatHealthTransition? RecordWriteFailure(string error)
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            _monitoringStartedUtc ??= now;

            var previousState = GetEffectiveStateUnsafe(now);
            _state = GetAgeUnsafe(now) > MissedWindowThreshold
                ? HeartbeatHealthState.MissedWindow
                : HeartbeatHealthState.WriteFailed;
            _lastWriteResult = error;

            return CreateTransitionUnsafe(previousState, _state, now);
        }
    }

    internal HmiHeartbeatHealthSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var now = timeProvider.GetUtcNow();
            var state = GetEffectiveStateUnsafe(now);
            return new HmiHeartbeatHealthSnapshot(
                state,
                GetAgeMsUnsafe(now),
                _lastWriteResult);
        }
    }

    private HmiHeartbeatHealthTransition? CreateTransitionUnsafe(
        HeartbeatHealthState previousState,
        HeartbeatHealthState currentState,
        DateTimeOffset now)
    {
        if (previousState == currentState)
        {
            return null;
        }

        return new HmiHeartbeatHealthTransition(
            previousState,
            currentState,
            GetAgeMsUnsafe(now),
            _lastWriteResult);
    }

    private HeartbeatHealthState GetEffectiveStateUnsafe(DateTimeOffset now)
    {
        return GetAgeUnsafe(now) > MissedWindowThreshold
            ? HeartbeatHealthState.MissedWindow
            : _state;
    }

    private TimeSpan GetAgeUnsafe(DateTimeOffset now)
    {
        var reference = _lastHeartbeatSuccessUtc ?? _monitoringStartedUtc;
        return reference is null ? TimeSpan.Zero : now - reference.Value;
    }

    private long? GetAgeMsUnsafe(DateTimeOffset now)
    {
        var reference = _lastHeartbeatSuccessUtc ?? _monitoringStartedUtc;
        return reference is null ? null : (long)(now - reference.Value).TotalMilliseconds;
    }
}
