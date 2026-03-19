using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class DiagnosticDispatcherOwnershipTests
{
    [Fact]
    public void MultiplePanels_StopOnlyAfterLastLease()
    {
        var dispatcher = new TestModbusDispatcher();
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);

        var firstPanel = ownership.AcquirePanelLease();
        Assert.True(firstPanel.ShouldStartDispatcher);

        dispatcher.SetStarted(true);

        var secondPanel = ownership.AcquirePanelLease();
        Assert.False(secondPanel.ShouldStartDispatcher);

        Assert.False(firstPanel.Release().ShouldStopDispatcher);
        Assert.True(secondPanel.Release().ShouldStopDispatcher);
    }

    [Fact]
    public void RuntimeOwnership_BlocksOldPanelFromStoppingDispatcher()
    {
        var dispatcher = new TestModbusDispatcher();
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);

        var panelLease = ownership.AcquirePanelLease();
        dispatcher.SetStarted(true);

        var runtimeLease = ownership.AcquireRuntimeLease();
        Assert.False(runtimeLease.ShouldStartDispatcher);

        runtimeLease.PromoteToPersistentRuntimeOwnership();

        Assert.False(panelLease.Release().ShouldStopDispatcher);
    }

    [Fact]
    public void ExternalStartedDispatcher_IsNotStoppedByPanelLease()
    {
        var dispatcher = new TestModbusDispatcher(started: true);
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);

        var panelLease = ownership.AcquirePanelLease();

        Assert.False(panelLease.ShouldStartDispatcher);
        Assert.False(panelLease.Release().ShouldStopDispatcher);
    }

    [Fact]
    public void FailedStart_DoesNotLeaveStaleTrackedSession()
    {
        var dispatcher = new TestModbusDispatcher();
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);

        var firstPanel = ownership.AcquirePanelLease();
        Assert.True(firstPanel.ShouldStartDispatcher);

        Assert.False(firstPanel.Release().ShouldStopDispatcher);

        var secondPanel = ownership.AcquirePanelLease();
        Assert.True(secondPanel.ShouldStartDispatcher);
    }

    private sealed class TestModbusDispatcher(bool started = false) : IModbusDispatcher
    {
        private event Func<Task>? Disconnecting;
        private event Action? Connected;
        private event Action? Stopped;
        private event Action<DiagnosticPingData>? PingDataUpdated;

        public bool IsConnected { get; private set; }
        public bool IsReconnecting { get; private set; }
        public bool IsStarted { get; private set; } = started;
        public DiagnosticPingData? LastPingData { get; private set; }

        event Func<Task>? IModbusDispatcher.Disconnecting
        {
            add => Disconnecting += value;
            remove => Disconnecting -= value;
        }

        event Action? IModbusDispatcher.Connected
        {
            add => Connected += value;
            remove => Connected -= value;
        }

        event Action? IModbusDispatcher.Stopped
        {
            add => Stopped += value;
            remove => Stopped -= value;
        }

        event Action<DiagnosticPingData>? IModbusDispatcher.PingDataUpdated
        {
            add => PingDataUpdated += value;
            remove => PingDataUpdated -= value;
        }

        public void SetStarted(bool startedValue)
        {
            IsStarted = startedValue;
        }

        ValueTask IModbusDispatcher.EnqueueAsync(IModbusCommand command, CancellationToken ct)
        {
            return ValueTask.CompletedTask;
        }

        Task IModbusDispatcher.StartAsync(CancellationToken ct)
        {
            IsStarted = true;
            Connected?.Invoke();
            return Task.CompletedTask;
        }

        async Task IModbusDispatcher.StopAsync()
        {
            if (Disconnecting != null)
            {
                await Disconnecting.Invoke();
            }

            IsStarted = false;
            IsConnected = false;
            IsReconnecting = false;
            LastPingData = null;
            Stopped?.Invoke();
        }

        ValueTask IAsyncDisposable.DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
