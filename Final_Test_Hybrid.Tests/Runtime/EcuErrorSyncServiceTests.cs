using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class EcuErrorSyncServiceTests
{
    [Fact]
    public void DispatcherStopped_ClearsActiveEcuErrorState()
    {
        var dispatcher = new TestModbusDispatcher();
        var errorService = new TestErrorService();
        using var service = new EcuErrorSyncService(
            dispatcher,
            errorService,
            TestInfrastructure.CreateDualLogger<EcuErrorSyncService>());

        dispatcher.PublishPing(new DiagnosticPingData
        {
            BoilerStatus = 1,
            LastErrorId = 1,
            ChTemperature = 110
        });
        dispatcher.RaiseStopped();

        Assert.Contains(ErrorDefinitions.EcuE9.Code, errorService.ClearedCodes);
    }

    private sealed class TestModbusDispatcher : IModbusDispatcher
    {
        private event Func<Task>? Disconnecting;
        private event Action? Connected;
        private event Action? Stopped;
        private event Action<DiagnosticPingData>? PingDataUpdated;

        public bool IsConnected => true;
        public bool IsReconnecting => false;
        public bool IsStarted => true;
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

        public void RaiseStopped()
        {
            Stopped?.Invoke();
        }

        public ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            Connected?.Invoke();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Disconnecting?.Invoke() ?? Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
