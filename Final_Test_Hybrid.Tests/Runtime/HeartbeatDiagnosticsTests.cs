using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Heartbeat;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class HeartbeatDiagnosticsTests
{
    [Fact]
    public void Monitor_StaysHealthy_AfterSuccessfulWrite()
    {
        var timeProvider = new ManualTimeProvider();
        var monitor = new HmiHeartbeatHealthMonitor(timeProvider);

        monitor.MarkMonitoringStarted();
        monitor.RecordWriteSuccess();

        var snapshot = monitor.GetSnapshot();

        Assert.Equal(HeartbeatHealthState.Healthy, snapshot.State);
        Assert.Equal("Успешно", snapshot.LastWriteResult);
    }

    [Fact]
    public void Monitor_SetsWriteFailed_OnWriteFailure_WithoutChangingOpcConnection()
    {
        var timeProvider = new ManualTimeProvider();
        var monitor = new HmiHeartbeatHealthMonitor(timeProvider);
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());

        connectionState.SetConnected(true, "test");
        monitor.MarkMonitoringStarted();
        monitor.RecordWriteFailure("BadRequestInterrupted");

        var snapshot = monitor.GetSnapshot();

        Assert.True(connectionState.IsConnected);
        Assert.Equal(HeartbeatHealthState.WriteFailed, snapshot.State);
        Assert.Equal("BadRequestInterrupted", snapshot.LastWriteResult);
    }

    [Fact]
    public void Monitor_SetsMissedWindow_WhenSuccessAgeExceedsThreshold()
    {
        var timeProvider = new ManualTimeProvider();
        var monitor = new HmiHeartbeatHealthMonitor(timeProvider);

        monitor.MarkMonitoringStarted();
        timeProvider.Advance(TimeSpan.FromSeconds(7));
        monitor.RecordWriteFailure("write timeout");

        var snapshot = monitor.GetSnapshot();

        Assert.Equal(HeartbeatHealthState.MissedWindow, snapshot.State);
        Assert.NotNull(snapshot.AgeMs);
        Assert.True(snapshot.AgeMs > 6000);
    }

    [Fact]
    public void Monitor_ReturnsHealthy_AfterSuccessFollowingFailure()
    {
        var timeProvider = new ManualTimeProvider();
        var monitor = new HmiHeartbeatHealthMonitor(timeProvider);

        monitor.MarkMonitoringStarted();
        monitor.RecordWriteFailure("BadRequestInterrupted");
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var transition = monitor.RecordWriteSuccess();
        var snapshot = monitor.GetSnapshot();

        Assert.NotNull(transition);
        Assert.Equal(HeartbeatHealthState.WriteFailed, transition.Value.PreviousState);
        Assert.Equal(HeartbeatHealthState.Healthy, transition.Value.CurrentState);
        Assert.Equal(HeartbeatHealthState.Healthy, snapshot.State);
        Assert.Equal("Успешно", snapshot.LastWriteResult);
    }

    [Fact]
    public async Task AutoReadyOff_Log_ContainsHeartbeatDetails()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var timeProvider = new ManualTimeProvider();
        var heartbeatMonitor = new HmiHeartbeatHealthMonitor(timeProvider);
        var connectionState = new OpcUaConnectionState(loggerFactory.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>(loggerFactory));
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            heartbeatMonitor,
            TestInfrastructure.CreateDualLogger<AutoReadySubscription>(loggerFactory));

        connectionState.SetConnected(true, "test");
        heartbeatMonitor.MarkMonitoringStarted();
        heartbeatMonitor.RecordWriteFailure("BadRequestInterrupted");

        await TestInfrastructure.InvokePrivateAsync(autoReady, "OnValueChanged", true);
        await TestInfrastructure.InvokePrivateAsync(autoReady, "OnValueChanged", false);

        Assert.Contains(
            logs.Entries,
            entry => entry.Level == LogLevel.Information
                && entry.Message.Contains("AutoReady OFF", StringComparison.Ordinal)
                && entry.Message.Contains("HeartbeatState=WriteFailed", StringComparison.Ordinal)
                && entry.Message.Contains("LastHeartbeatWriteResult=BadRequestInterrupted", StringComparison.Ordinal)
                && entry.Message.Contains("HeartbeatAgeMs=", StringComparison.Ordinal));
    }

    private static ILoggerFactory CreateLoggerFactory(out RecordingLoggerProvider provider)
    {
        var recordingProvider = new RecordingLoggerProvider();
        provider = recordingProvider;
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(recordingProvider);
        });
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = DateTimeOffset.Parse("2026-03-22T12:00:00Z");

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }

        public void Advance(TimeSpan delta)
        {
            _utcNow = _utcNow.Add(delta);
        }
    }
}
