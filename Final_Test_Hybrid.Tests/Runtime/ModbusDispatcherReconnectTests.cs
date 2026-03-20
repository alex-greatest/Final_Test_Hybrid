using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue.Internal;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;
using NModbus;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class ModbusDispatcherReconnectTests
{
    [Fact]
    public async Task ReconnectDrain_FaultsPendingCommandsInBothQueues()
    {
        var queue = CreateQueue();
        var highCommand = new TestCommand(CommandPriority.High, "Runtime.Step");
        var lowCommand = new TestCommand(CommandPriority.Low, "Runtime.Step");

        Assert.True(queue.HighQueue!.Writer.TryWrite(highCommand));
        Assert.True(queue.LowQueue!.Writer.TryWrite(lowCommand));

        queue.FailPendingCommandsOnReconnect(ModbusReconnectExceptionFactory.CreateForPendingCommand);

        await AssertReconnectFaultAsync(highCommand.Completion);
        await AssertReconnectFaultAsync(lowCommand.Completion);
    }

    [Fact]
    public async Task StopCancel_KeepsCancelPathForPendingCommands()
    {
        var queue = CreateQueue();
        var command = new TestCommand(CommandPriority.High, "Runtime.Step");

        Assert.True(queue.HighQueue!.Writer.TryWrite(command));

        queue.CancelAllPendingCommands();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.Completion);
    }

    [Fact]
    public void ReconnectDrain_DoesNotTouchAlreadyDequeuedCommand()
    {
        var queue = CreateQueue();
        var command = new TestCommand(CommandPriority.High, "Runtime.Step");

        Assert.True(queue.HighQueue!.Writer.TryWrite(command));
        Assert.True(queue.HighQueue.Reader.TryRead(out var dequeued));

        queue.FailPendingCommandsOnReconnect(ModbusReconnectExceptionFactory.CreateForPendingCommand);

        Assert.Same(command, dequeued);
        Assert.False(command.Completion.IsCompleted);
    }

    [Fact]
    public async Task Dispatcher_RejectsDuringReconnect_AndAcceptsAfterQueueRestore()
    {
        var dispatcher = CreateDispatcher();
        var queue = TestInfrastructure.GetPrivateField<ModbusCommandQueue>(dispatcher, "_commandQueue");
        var pendingCommand = new TestCommand(CommandPriority.High, "Runtime.Step");

        Assert.True(queue.HighQueue!.Writer.TryWrite(pendingCommand));

        await TestInfrastructure.InvokePrivateAsync(
            dispatcher,
            "HandleConnectionLostAsync",
            CancellationToken.None);

        await AssertReconnectFaultAsync(pendingCommand.Completion);

        var rejectedCommand = new TestCommand(CommandPriority.High, "Runtime.Step");
        await dispatcher.EnqueueAsync(rejectedCommand, CancellationToken.None);
        await AssertReconnectFaultAsync(rejectedCommand.Completion);

        TestInfrastructure.SetPrivateField(dispatcher, "_isReconnecting", false);
        TestInfrastructure.InvokePrivate(dispatcher, "SetPortOpenState", true);

        var acceptedCommand = new TestCommand(CommandPriority.High, "Runtime.Step");
        await dispatcher.EnqueueAsync(acceptedCommand, CancellationToken.None);

        Assert.False(acceptedCommand.Completion.IsCompleted);
        Assert.True(queue.HighQueue!.Reader.TryRead(out var queuedCommand));
        Assert.Same(acceptedCommand, queuedCommand);
    }

    [Fact]
    public async Task Dispatcher_ReconnectFaultsWriterWaitingOnFullQueue()
    {
        var dispatcher = CreateDispatcher(new ModbusDispatcherOptions
        {
            HighPriorityQueueCapacity = 1,
            LowPriorityQueueCapacity = 1
        });
        var queue = TestInfrastructure.GetPrivateField<ModbusCommandQueue>(dispatcher, "_commandQueue");
        var queuedCommand = new TestCommand(CommandPriority.High, "Runtime.Step");
        var waitingCommand = new TestCommand(CommandPriority.High, "Runtime.Step");

        Assert.True(queue.HighQueue!.Writer.TryWrite(queuedCommand));

        var enqueueTask = dispatcher.EnqueueAsync(waitingCommand, CancellationToken.None).AsTask();
        await Task.Delay(50);
        Assert.False(enqueueTask.IsCompleted);

        await TestInfrastructure.InvokePrivateAsync(
            dispatcher,
            "HandleConnectionLostAsync",
            CancellationToken.None);

        TestInfrastructure.SetPrivateField(dispatcher, "_isReconnecting", false);
        TestInfrastructure.InvokePrivate(dispatcher, "SetPortOpenState", true);

        await enqueueTask;
        await AssertReconnectFaultAsync(waitingCommand.Completion);
    }

    private static ModbusCommandQueue CreateQueue()
    {
        var queue = new ModbusCommandQueue();
        queue.RecreateChannels(new ModbusDispatcherOptions());
        return queue;
    }

    private static ModbusDispatcher CreateDispatcher(ModbusDispatcherOptions? dispatcherOptions = null)
    {
        var diagnosticSettings = Options.Create(new DiagnosticSettings
        {
            PortName = "COM255",
            BaseAddressOffset = 1
        });

        var connectionManager = new ModbusConnectionManager(
            diagnosticSettings,
            TestInfrastructure.CreateLogger<ModbusConnectionManager>());

        return new ModbusDispatcher(
            connectionManager,
            Options.Create(dispatcherOptions ?? new ModbusDispatcherOptions()),
            diagnosticSettings,
            new ExecutionActivityTracker(),
            TestInfrastructure.CreateLogger<ModbusDispatcher>(),
            new TestStepLoggerStub());
    }

    private static async Task AssertReconnectFaultAsync(Task task)
    {
        var ex = await Assert.ThrowsAsync<IOException>(() => task);
        Assert.Contains("переподключение Modbus", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestCommand(CommandPriority priority, string source) : ModbusCommandBase(priority, "TestCommand", "Doc=1005", source, CancellationToken.None)
    {
        public Task Completion => Task;

        protected override Task ExecuteCoreAsync(IModbusMaster master, byte slaveId, CancellationToken ct)
        {
            return Task.CompletedTask;
        }
    }
}
