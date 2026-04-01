using System.Reflection;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class TestExecutionCoordinatorCompletionTests
{
    [Fact]
    public async Task CompleteAsync_OperatorStop_WaitsForRetentionCleanupBeforeSequenceCompleted()
    {
        var service = CreateRefreshService();
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var coordinator = CreateCoordinator(service, ExecutionStopReason.Operator);
        var sequenceCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnSequenceCompleted += () => sequenceCompleted.TrySetResult();

        var completeTask = TestInfrastructure.InvokePrivateAsync(coordinator, "CompleteAsync");

        await Task.Delay(60);
        Assert.False(completeTask.IsCompleted);
        Assert.False(sequenceCompleted.Task.IsCompleted);

        heldLease.Dispose();
        await completeTask;
        await sequenceCompleted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Dispose();
    }

    [Theory]
    [InlineData(ExecutionStopReason.PlcForceStop)]
    [InlineData(ExecutionStopReason.PlcSoftReset)]
    [InlineData(ExecutionStopReason.PlcHardReset)]
    public async Task CompleteAsync_NonOperatorStop_DoesNotWaitForRetentionCleanup(ExecutionStopReason reason)
    {
        var service = CreateRefreshService();
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var coordinator = CreateCoordinator(service, reason);
        var sequenceCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnSequenceCompleted += () => sequenceCompleted.TrySetResult();

        await TestInfrastructure.InvokePrivateAsync(coordinator, "CompleteAsync").WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(sequenceCompleted.Task.IsCompleted);
        heldLease.Dispose();
        service.Dispose();
    }

    [Fact]
    public async Task CompleteAsync_NormalCompletion_DoesNotWaitForRetentionCleanup()
    {
        var service = CreateRefreshService();
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var coordinator = CreateCoordinator(service, reason: null);
        var sequenceCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.OnSequenceCompleted += () => sequenceCompleted.TrySetResult();

        await TestInfrastructure.InvokePrivateAsync(coordinator, "CompleteAsync").WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(sequenceCompleted.Task.IsCompleted);
        heldLease.Dispose();
        service.Dispose();
    }

    private static BoilerOperationModeRefreshService CreateRefreshService()
    {
        var dispatcher = new CompletionTestModbusDispatcher();
        var modbusClient = new CompletionTestModbusClient();
        var writer = new RegisterWriter(modbusClient, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());
        var reader = new RegisterReader(modbusClient, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        var settings = new DiagnosticSettings
        {
            BaseAddressOffset = 1,
            WriteVerifyDelayMs = 10
        };

        return new BoilerOperationModeRefreshService(
            dispatcher,
            writer,
            reader,
            settings,
            TestInfrastructure.CreateDualLogger<BoilerOperationModeRefreshService>(),
            refreshInterval: TimeSpan.FromSeconds(5),
            dispatcherPollInterval: TimeSpan.FromMilliseconds(20),
            failedRefreshRetryDelay: TimeSpan.FromMilliseconds(20));
    }

    private static TestExecutionCoordinator CreateCoordinator(
        BoilerOperationModeRefreshService service,
        ExecutionStopReason? reason)
    {
        var coordinator = (TestExecutionCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(TestExecutionCoordinator));
        var activityTracker = new ExecutionActivityTracker();
        var flowState = new ExecutionFlowState();
        var stateManager = new ExecutionStateManager();

        if (reason.HasValue)
        {
            var stopAsFailure = reason.Value is ExecutionStopReason.PlcForceStop
                or ExecutionStopReason.PlcSoftReset
                or ExecutionStopReason.PlcHardReset;
            flowState.RequestStop(reason.Value, stopAsFailure);
        }

        activityTracker.SetTestExecutionActive(true);
        stateManager.TransitionTo(ExecutionState.Running);

        SetField(coordinator, "_logger", TestInfrastructure.CreateLogger<TestExecutionCoordinator>());
        SetField(coordinator, "_testLogger", new TestStepLoggerStub());
        SetField(coordinator, "_errorService", new TestErrorService());
        SetField(coordinator, "_activityTracker", activityTracker);
        SetField(coordinator, "_flowState", flowState);
        SetField(coordinator, "_boilerOperationModeRefreshService", service);
        SetField(coordinator, "_stateLock", new Lock());
        SetField(coordinator, "_cts", new CancellationTokenSource());
        SetField(coordinator, "<StateManager>k__BackingField", stateManager);
        return coordinator;
    }

    private static void SetField<T>(object instance, string name, T value)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic));
        field.SetValue(instance, value);
    }

    private sealed class CompletionTestModbusDispatcher : IModbusDispatcher
    {
        public bool IsConnected => true;
        public bool IsReconnecting => false;
        public bool IsStarted => true;
        public DiagnosticPingData? LastPingData { get; } = new();

        public event Func<Task>? Disconnecting
        {
            add { }
            remove { }
        }

        public event Action? Connected
        {
            add { }
            remove { }
        }

        public event Action? Stopped
        {
            add { }
            remove { }
        }

        public event Action<DiagnosticPingData>? PingDataUpdated
        {
            add { }
            remove { }
        }

        public ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class CompletionTestModbusClient : IModbusClient
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ushort, ushort> _registers = [];

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                var values = new ushort[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = _registers.GetValueOrDefault((ushort)(address + i));
                }

                return Task.FromResult(values);
            }
        }

        public Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                _registers[address] = value;
            }

            return Task.CompletedTask;
        }

        public Task WriteMultipleRegistersAsync(
            ushort address,
            ushort[] values,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                for (var i = 0; i < values.Length; i++)
                {
                    _registers[(ushort)(address + i)] = values[i];
                }
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
