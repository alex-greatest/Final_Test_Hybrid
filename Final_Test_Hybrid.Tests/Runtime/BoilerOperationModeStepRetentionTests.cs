using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Steps.Coms;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Opc.Ua.Client;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BoilerOperationModeStepRetentionTests
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan DispatcherPollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);

    [Fact]
    public async Task ChStartStHeatoutStep_ArmsRetentionAfterConfirmed1036()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChStartStHeatoutStep(
            service,
            Options.Create(CreateDiagnosticSettings()),
            TestInfrastructure.CreateDualLogger<ChStartStHeatoutStep>());

        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([4, 4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartStHeatoutStep_WaitsForSharedLeaseBefore1036Write()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        var context = CreateContext(modbusClient);
        var step = new ChStartStHeatoutStep(
            service,
            Options.Create(CreateDiagnosticSettings()),
            TestInfrastructure.CreateDualLogger<ChStartStHeatoutStep>());

        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var task = step.ExecuteAsync(context, CancellationToken.None);

        await Task.Delay(120);
        Assert.False(task.IsCompleted);
        Assert.Empty(modbusClient.WrittenValues);

        heldLease.Dispose();
        var result = await task;

        Assert.True(result.Success);
        Assert.Equal([4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartStHeatoutStep_ClearsPreviousRetentionAtEntry()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient
        {
            FailReadsRemaining = 1,
            ReadFailureMessage = "forced read failure"
        };
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChStartStHeatoutStep(
            service,
            Options.Create(CreateDiagnosticSettings()),
            TestInfrastructure.CreateDualLogger<ChStartStHeatoutStep>());

        service.ArmMode(3, "old-mode");
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        await Task.Delay(180);
        Assert.Equal([4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMaxHeatoutStep_ArmsRetentionImmediatelyAfterReady1Verify()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(
            modbusClient,
            "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Error\"");
        var step = new ChStartMaxHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMaxHeatoutStep>());

        var result = await InvokeReady1Async(step, context);

        Assert.False(result.Success);
        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([4, 4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMaxHeatoutStep_WaitsForSharedLeaseBefore1036Write()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        var context = CreateContext(
            modbusClient,
            "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Max_Heatout\".\"Error\"");
        var step = new ChStartMaxHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMaxHeatoutStep>());

        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var task = InvokeReady1Async(step, context);

        await Task.Delay(120);
        Assert.False(task.IsCompleted);
        Assert.Empty(modbusClient.WrittenValues);

        heldLease.Dispose();
        var result = await task;

        Assert.False(result.Success);
        Assert.Equal([4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMaxHeatoutStep_ClearsPreviousRetentionAtEntry()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChStartMaxHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMaxHeatoutStep>());

        service.ArmMode(3, "old-mode");
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        await Task.Delay(180);
        Assert.Empty(modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMinHeatoutStep_ArmsRetentionImmediatelyAfterReady1Verify()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(
            modbusClient,
            "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Error\"");
        var step = new ChStartMinHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMinHeatoutStep>());

        var result = await InvokeReady1Async(step, context);

        Assert.False(result.Success);
        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([3, 3], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMinHeatoutStep_WaitsForSharedLeaseBefore1036Write()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        var context = CreateContext(
            modbusClient,
            "ns=3;s=\"DB_VI\".\"Coms\".\"CH_Start_Min_Heatout\".\"Error\"");
        var step = new ChStartMinHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMinHeatoutStep>());

        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var task = InvokeReady1Async(step, context);

        await Task.Delay(120);
        Assert.False(task.IsCompleted);
        Assert.Empty(modbusClient.WrittenValues);

        heldLease.Dispose();
        var result = await task;

        Assert.False(result.Success);
        Assert.Equal([3], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChStartMinHeatoutStep_ClearsPreviousRetentionAtEntry()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChStartMinHeatoutStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChStartMinHeatoutStep>());

        service.ArmMode(4, "old-mode");
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        await Task.Delay(180);
        Assert.Empty(modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChResetStep_ClearsRetentionOnlyAfterConfirmedZero()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChResetStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChResetStep>());

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.True(result.Success);
        await Task.Delay(180);
        Assert.Equal([0], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChResetStep_DoesNotClearRetention_WhenReadBackFailed()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient
        {
            FailReadsRemaining = 1,
            ReadFailureMessage = "forced read failure"
        };
        using var service = CreateRefreshService(dispatcher, modbusClient);
        var context = CreateContext(modbusClient);
        var step = new ChResetStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChResetStep>());

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var result = await step.ExecuteAsync(context, CancellationToken.None);

        Assert.False(result.Success);
        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([0, 4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ChResetStep_WaitsForSharedLeaseBeforeWritingZero()
    {
        var dispatcher = new TestModbusDispatcher();
        var modbusClient = new StepModbusClient();
        using var service = CreateRefreshService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        var context = CreateContext(modbusClient);
        var step = new ChResetStep(
            accessLevelManager: null!,
            dispatcher: dispatcher,
            boilerOperationModeRefreshService: service,
            settings: Options.Create(CreateDiagnosticSettings()),
            logger: TestInfrastructure.CreateDualLogger<ChResetStep>());

        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var task = step.ExecuteAsync(context, CancellationToken.None);

        await Task.Delay(120);
        Assert.False(task.IsCompleted);
        Assert.Empty(modbusClient.WrittenValues);

        heldLease.Dispose();
        var result = await task;

        Assert.True(result.Success);
        Assert.Equal([0], modbusClient.WrittenValues);
    }

    private static BoilerOperationModeRefreshService CreateRefreshService(
        TestModbusDispatcher dispatcher,
        StepModbusClient modbusClient,
        TimeSpan? refreshInterval = null,
        TimeSpan? dispatcherPollInterval = null,
        TimeSpan? retryDelay = null)
    {
        var settings = CreateDiagnosticSettings();
        var writer = new RegisterWriter(modbusClient, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());
        var reader = new RegisterReader(modbusClient, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        return new BoilerOperationModeRefreshService(
            dispatcher,
            writer,
            reader,
            settings,
            TestInfrastructure.CreateDualLogger<BoilerOperationModeRefreshService>(),
            refreshInterval ?? RefreshInterval,
            dispatcherPollInterval ?? DispatcherPollInterval,
            retryDelay ?? RetryDelay);
    }

    private static TestStepContext CreateContext(StepModbusClient modbusClient)
    {
        return CreateContext(modbusClient, errorTag: null);
    }

    private static TestStepContext CreateContext(StepModbusClient modbusClient, string? errorTag)
    {
        var pauseToken = new PauseTokenSource();
        var writer = new RegisterWriter(modbusClient, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());
        var reader = new RegisterReader(modbusClient, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        var tagWaiter = CreateTagWaiter(pauseToken, errorTag);

        return new TestStepContext(
            columnIndex: 0,
            stepPacingWindow: TimeSpan.Zero,
            opcUa: CreatePausableOpcUa(pauseToken),
            logger: TestInfrastructure.CreateLogger<TestStepContext>(),
            recipeProvider: null!,
            pauseToken: pauseToken,
            diagReader: new PausableRegisterReader(reader, pauseToken),
            diagWriter: new PausableRegisterWriter(writer, pauseToken),
            tagWaiter: tagWaiter,
            rangeSliderUiState: null!);
    }

    private static PausableOpcUaTagService CreatePausableOpcUa(PauseTokenSource pauseToken)
    {
        var connectionService = (OpcUaConnectionService)RuntimeHelpers.GetUninitializedObject(typeof(OpcUaConnectionService));
        var opcUaTagService = new OpcUaTagService(
            connectionService,
            TestInfrastructure.CreateDualLogger<OpcUaTagService>());

        return new PausableOpcUaTagService(
            opcUaTagService,
            TestInfrastructure.CreateSubscription(),
            pauseToken);
    }

    private static PausableTagWaiter CreateTagWaiter(PauseTokenSource pauseToken, string? errorTag)
    {
        if (errorTag == null)
        {
            return new PausableTagWaiter(
                (TagWaiter)RuntimeHelpers.GetUninitializedObject(typeof(TagWaiter)),
                pauseToken);
        }

        var subscription = TestInfrastructure.CreateSubscription();
        RegisterTrackedNode(subscription, errorTag);
        TestInfrastructure.SetSubscriptionValue(subscription, errorTag, true, updateSequence: 1);
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var inner = new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());

        return new PausableTagWaiter(
            inner,
            pauseToken);
    }

    private static DiagnosticSettings CreateDiagnosticSettings()
    {
        return new DiagnosticSettings
        {
            BaseAddressOffset = 1,
            WriteVerifyDelayMs = (int)RetryDelay.TotalMilliseconds
        };
    }

    private static async Task<TestStepResult> InvokeReady1Async<TStep>(TStep step, TestStepContext context)
    {
        var task = Assert.IsAssignableFrom<Task<TestStepResult>>(
            TestInfrastructure.InvokePrivate(step!, "HandleReady1Async", context, CancellationToken.None));
        return await task;
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadlineUtc = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadlineUtc)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(predicate(), "Условие не выполнилось в отведённое время.");
    }

    private static void RegisterTrackedNode(OpcUaSubscription subscription, string nodeId)
    {
        var monitoredItems = TestInfrastructure.GetPrivateField<ConcurrentDictionary<string, MonitoredItem>>(subscription, "_monitoredItems");
        monitoredItems[nodeId] = (MonitoredItem)RuntimeHelpers.GetUninitializedObject(typeof(MonitoredItem));
    }

    private sealed class TestModbusDispatcher : IModbusDispatcher
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

    private sealed class StepModbusClient : IModbusClient
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ushort, ushort> _registers = [];

        public int FailReadsRemaining { get; set; }
        public string ReadFailureMessage { get; set; } = "read failure";
        public int WriteSingleRegisterCalls { get; private set; }
        public List<ushort> WrittenValues { get; } = [];

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (FailReadsRemaining > 0)
                {
                    FailReadsRemaining--;
                    throw new InvalidOperationException(ReadFailureMessage);
                }

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
                WriteSingleRegisterCalls++;
                WrittenValues.Add(value);
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
