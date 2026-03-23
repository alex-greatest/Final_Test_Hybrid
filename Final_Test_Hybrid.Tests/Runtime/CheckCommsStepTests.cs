using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class CheckCommsStepTests
{
    [Fact]
    public async Task ExecuteAsync_WaitsForFreshRuntimePing_WhenSharedDispatcherHasStalePanelPing()
    {
        var dispatcher = new TestModbusDispatcher(started: true);
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);
        var panelLease = ownership.AcquirePanelLease();
        var autoReady = CreateAutoReady();
        var phaseState = new ExecutionPhaseState();
        var step = new CheckCommsStep(
            dispatcher,
            ownership,
            autoReady,
            phaseState,
            TestInfrastructure.CreateDualLogger<CheckCommsStep>());
        var stalePing = new DiagnosticPingData { ModeKey = 1, BoilerStatus = 2 };
        dispatcher.PublishPing(stalePing);
        await SetAutoReadyAsync(autoReady, true);

        var executeTask = step.ExecuteAsync(CreateContext(), CancellationToken.None);
        await Task.Delay(100);

        Assert.False(executeTask.IsCompleted);

        dispatcher.PublishPing(new DiagnosticPingData { ModeKey = 1, BoilerStatus = 2 });

        var result = await executeTask.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.Success);
        Assert.False(result.Skipped);
        Assert.Equal(0, dispatcher.StartCalls);
        Assert.Equal(0, dispatcher.StopCalls);
        Assert.Null(phaseState.Phase);

        panelLease.Release();
        await dispatcher.StopAsync();
        ownership.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_StopsSharedDispatcher_WhenAutoReadyIsFalse()
    {
        var dispatcher = new TestModbusDispatcher(started: true);
        var ownership = new DiagnosticDispatcherOwnership(dispatcher);
        var panelLease = ownership.AcquirePanelLease();
        var autoReady = CreateAutoReady();
        var phaseState = new ExecutionPhaseState();
        var step = new CheckCommsStep(
            dispatcher,
            ownership,
            autoReady,
            phaseState,
            TestInfrastructure.CreateDualLogger<CheckCommsStep>());
        await SetAutoReadyAsync(autoReady, false);

        var result = await step.ExecuteAsync(CreateContext(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Нет связи с котлом", result.Message);
        var error = Assert.Single(result.Errors!);
        Assert.Equal(ErrorDefinitions.NoDiagnosticConnection.Code, error.Code);
        Assert.Equal(1, dispatcher.StopCalls);
        Assert.False(dispatcher.IsStarted);
        Assert.Null(phaseState.Phase);

        panelLease.Release();
        ownership.Dispose();
    }

    private static AutoReadySubscription CreateAutoReady()
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        return TestInfrastructure.CreateAutoReadySubscription(subscription, connectionState);
    }

    private static async Task SetAutoReadyAsync(AutoReadySubscription autoReady, bool isReady)
    {
        await TestInfrastructure.InvokePrivateAsync(autoReady, "OnValueChanged", isReady);
    }

    private static TestStepContext CreateContext()
    {
        var pauseToken = new PauseTokenSource();
        return new TestStepContext(
            columnIndex: 0,
            stepPacingWindow: TimeSpan.Zero,
            opcUa: null!,
            logger: TestInfrastructure.CreateLogger<TestStepContext>(),
            recipeProvider: null!,
            pauseToken: pauseToken,
            diagReader: null!,
            diagWriter: null!,
            tagWaiter: null!,
            rangeSliderUiState: null!);
    }

    private sealed class TestModbusDispatcher(bool started) : IModbusDispatcher
    {
        private event Func<Task>? Disconnecting;
        private event Action? Connected;
        private event Action? Stopped;
        private event Action<DiagnosticPingData>? PingDataUpdated;

        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
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

        public void PublishPing(DiagnosticPingData data)
        {
            LastPingData = data;
            PingDataUpdated?.Invoke(data);
        }

        public ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            StartCalls++;
            IsStarted = true;
            IsConnected = true;
            Connected?.Invoke();
            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            StopCalls++;
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

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
