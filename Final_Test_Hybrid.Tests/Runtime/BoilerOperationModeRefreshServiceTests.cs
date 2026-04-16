using System.Reflection;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Diagnostic.Connection;
using Final_Test_Hybrid.Services.Diagnostic.Models;
using Final_Test_Hybrid.Services.Diagnostic.Protocol;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.SpringBoot.Recipe;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Recipe;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BoilerOperationModeRefreshServiceTests
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(90);
    private static readonly TimeSpan DispatcherPollInterval = TimeSpan.FromMilliseconds(20);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(25);
    private static readonly TimeSpan SlowFailedRefreshRetryDelay = TimeSpan.FromMilliseconds(120);

    [Fact]
    public async Task ArmMode_RewritesModeAfterRefreshInterval_AndRestartsIntervalFromSuccessfulWrite()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);

        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await Task.Delay(40);
        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([3, 4], modbusClient.WrittenValues);

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 4, TimeSpan.FromSeconds(1));
        Assert.Equal([3, 4, 3, 4], modbusClient.WrittenValues);
        Assert.Equal(0, dispatcher.StartCalls);
    }

    [Fact]
    public async Task ArmMode_WaitsForDispatcherReady_AndDoesNotCallStartAsync()
    {
        var dispatcher = new TestModbusDispatcher(ready: false);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);

        service.ArmMode(3, "Coms/CH_Start_Min_Heatout");

        await Task.Delay(160);
        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);

        dispatcher.SetReady();

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([4, 3], modbusClient.WrittenValues);
        Assert.Equal(0, dispatcher.StartCalls);
    }

    [Fact]
    public async Task ArmMode_UsesConfiguredRefreshInterval_WhenCtorOverrideIsNotProvided()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var settings = CreateDiagnosticSettings(operationModeRefreshInterval: TimeSpan.FromMilliseconds(35));
        var writer = new RegisterWriter(modbusClient, TestInfrastructure.CreateLogger<RegisterWriter>(), new TestStepLoggerStub());
        var reader = new RegisterReader(modbusClient, TestInfrastructure.CreateLogger<RegisterReader>(), new TestStepLoggerStub());
        using var service = new BoilerOperationModeRefreshService(
            dispatcher,
            writer,
            reader,
            settings,
            TestInfrastructure.CreateDualLogger<BoilerOperationModeRefreshService>(),
            refreshInterval: null,
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: RetryDelay);

        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await Task.Delay(15);
        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        Assert.Equal([3, 4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ArmMode_UsesSingleWriteFallback_ForUnknownTargetMode()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);

        service.ArmMode(7, "unknown-mode");

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 1, TimeSpan.FromSeconds(1));
        Assert.Equal([7], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task Clear_CancelsPendingRefreshWithoutLateWrite()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await Task.Delay(30);
        service.Clear("manual test");

        await Task.Delay(180);

        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task ClearAndDrainAsync_WaitsUntilActiveRefreshCriticalSectionFinishes()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new BlockingRefreshModbusClient();
        using var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(30),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: RetryDelay);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await modbusClient.WaitForWriteStartedAsync(TimeSpan.FromSeconds(1));

        var clearTask = service.ClearAndDrainAsync("awaited clear");

        await Task.Delay(60);
        Assert.False(clearTask.IsCompleted);

        modbusClient.ReleaseWrite();
        await clearTask;
    }

    [Fact]
    public async Task ClearAndDrainAsync_InvalidatesStateBeforeDrain_AndDoesNotAllowSecondRefresh()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new BlockingRefreshModbusClient();
        using var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(30),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: RetryDelay);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await modbusClient.WaitForWriteStartedAsync(TimeSpan.FromSeconds(1));

        var clearTask = service.ClearAndDrainAsync("awaited clear");
        modbusClient.ReleaseWrite();
        await clearTask;
        await Task.Delay(120);

        Assert.Equal(2, modbusClient.WriteSingleRegisterCalls);
        Assert.Equal([3, 4], modbusClient.WrittenValues);
    }

    [Fact]
    public async Task ClearWhileRefreshWaitsForSharedLease_DoesNotAllowLateWrite()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await Task.Delay(RefreshInterval + TimeSpan.FromMilliseconds(40));
        service.Clear("clear during queued refresh");

        heldLease.Dispose();
        await Task.Delay(180);

        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task ClearDuringTwoStepRefresh_CompletesTargetWrite_WithoutLateRetry()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var serviceHolder = new RefreshServiceHolder();
        modbusClient.AfterWriteCall = callNumber =>
        {
            if (callNumber == 1)
            {
                serviceHolder.Instance!.Clear("clear after opposite write");
            }
        };

        using var service = CreateService(dispatcher, modbusClient);
        serviceHolder.Instance = service;
        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
        await Task.Delay(180);

        Assert.Equal([3, 4], modbusClient.WrittenValues);
        Assert.Equal(2, modbusClient.WriteSingleRegisterCalls);
    }

    private sealed class RefreshServiceHolder
    {
        public BoilerOperationModeRefreshService? Instance { get; set; }
    }

    [Fact]
    public async Task BoilerStateClear_CancelsPendingRefresh()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var errorCoordinator = new RecordingErrorCoordinator();
        var boilerState = CreateBoilerState();
        var plcResetCoordinator = CreateUninitializedPlcResetCoordinator();
        using var service = CreateService(dispatcher, modbusClient, plcResetCoordinator, errorCoordinator, boilerState);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await Task.Delay(30);
        boilerState.Clear();

        await Task.Delay(180);

        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task ErrorCoordinatorReset_CancelsPendingRefresh()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var errorCoordinator = new RecordingErrorCoordinator();
        var boilerState = CreateBoilerState();
        var plcResetCoordinator = CreateUninitializedPlcResetCoordinator();
        using var service = CreateService(dispatcher, modbusClient, plcResetCoordinator, errorCoordinator, boilerState);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await Task.Delay(30);
        errorCoordinator.RaiseReset();

        await Task.Delay(180);

        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task PlcResetForceStop_CancelsPendingRefresh()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var errorCoordinator = new RecordingErrorCoordinator();
        var boilerState = CreateBoilerState();
        var plcResetCoordinator = CreateUninitializedPlcResetCoordinator();
        using var service = CreateService(dispatcher, modbusClient, plcResetCoordinator, errorCoordinator, boilerState);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await Task.Delay(30);
        RaisePlcForceStop(plcResetCoordinator);

        await Task.Delay(180);

        Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);
    }

    [Fact]
    public async Task Clear_SchedulesSingleBackgroundDrain_WithoutTaskLeak()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);

        service.Clear("first clear");
        var firstDrain = GetBackgroundDrainTask(service);

        service.Clear("second clear");
        var secondDrain = GetBackgroundDrainTask(service);

        Assert.Same(firstDrain, secondDrain);
        Assert.False(firstDrain.IsCompleted);

        heldLease.Dispose();
        await WaitUntilAsync(() => firstDrain.IsCompleted, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Clear_FromLeaseHeldPath_DoesNotDeadlock()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);

        Exception? exception = null;
        try
        {
            service.Clear("lease-held clear");
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        var backgroundDrain = GetBackgroundDrainTask(service);

        Assert.Null(exception);
        Assert.False(backgroundDrain.IsCompleted);

        heldLease.Dispose();
        await WaitUntilAsync(() => backgroundDrain.IsCompleted, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Dispose_WithPendingBackgroundDrain_DoesNotThrow_AndStopsCleanly()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new BlockingRefreshModbusClient();
        var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(30),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: RetryDelay);

        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        await modbusClient.WaitForWriteStartedAsync(TimeSpan.FromSeconds(1));
        service.Clear("pending background drain");

        var exception = Record.Exception(service.Dispose);

        Assert.Null(exception);

        modbusClient.ReleaseWrite();
    }

    [Fact]
    public async Task RefreshSkipsStaleSnapshotWhileModeChangeLeaseIsHeld()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);
        var modeChangeLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        var leaseReleased = false;

        try
        {
            service.ArmMode(4, "old-mode");
            await Task.Delay(RefreshInterval + TimeSpan.FromMilliseconds(40));
            service.ArmMode(3, "new-mode");

            modeChangeLease.Dispose();
            leaseReleased = true;

            await Task.Delay(40);
            Assert.Equal(0, modbusClient.WriteSingleRegisterCalls);

            await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 2, TimeSpan.FromSeconds(1));
            Assert.Equal([4, 3], modbusClient.WrittenValues);
        }
        finally
        {
            if (!leaseReleased)
            {
                modeChangeLease.Dispose();
            }
        }
    }

    [Fact]
    public async Task FailedRefresh_UsesDedicatedSlowRetryInsteadOfWriteVerifyDelay()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient
        {
            ForcedReadValue = 0
        };
        using var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(50),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: SlowFailedRefreshRetryDelay,
            writeVerifyDelayMs: 5);

        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 4, TimeSpan.FromSeconds(2));
        var retryDelta = modbusClient.WriteMomentsUtc[2] - modbusClient.WriteMomentsUtc[1];

        Assert.True(
            retryDelta >= SlowFailedRefreshRetryDelay - TimeSpan.FromMilliseconds(25),
            $"Ожидался slow retry не раньше {SlowFailedRefreshRetryDelay.TotalMilliseconds} мс, фактически {retryDelta.TotalMilliseconds} мс.");
    }

    [Fact]
    public async Task FailedRefresh_WhenIntermediateWriteFails_UsesDedicatedSlowRetry()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        modbusClient.FailWriteCallNumbers.Add(1);
        using var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(50),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: SlowFailedRefreshRetryDelay);

        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 3, TimeSpan.FromSeconds(2));
        Assert.Equal([3, 3, 4], modbusClient.WrittenValues);

        var retryDelta = modbusClient.WriteMomentsUtc[1] - modbusClient.WriteMomentsUtc[0];
        Assert.True(
            retryDelta >= SlowFailedRefreshRetryDelay - TimeSpan.FromMilliseconds(25),
            $"Ожидался slow retry не раньше {SlowFailedRefreshRetryDelay.TotalMilliseconds} мс, фактически {retryDelta.TotalMilliseconds} мс.");
    }

    [Fact]
    public async Task FailedRefresh_WhenTargetWriteFails_UsesDedicatedSlowRetry()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        modbusClient.FailWriteCallNumbers.Add(2);
        using var service = CreateService(
            dispatcher,
            modbusClient,
            refreshInterval: TimeSpan.FromMilliseconds(50),
            dispatcherPollInterval: DispatcherPollInterval,
            failedRefreshRetryDelay: SlowFailedRefreshRetryDelay);

        service.ArmMode(4, "Coms/CH_Start_Max_Heatout");

        await WaitUntilAsync(() => modbusClient.WriteSingleRegisterCalls >= 4, TimeSpan.FromSeconds(2));
        Assert.Equal([3, 4, 3, 4], modbusClient.WrittenValues);

        var retryDelta = modbusClient.WriteMomentsUtc[2] - modbusClient.WriteMomentsUtc[1];
        Assert.True(
            retryDelta >= SlowFailedRefreshRetryDelay - TimeSpan.FromMilliseconds(25),
            $"Ожидался slow retry не раньше {SlowFailedRefreshRetryDelay.TotalMilliseconds} мс, фактически {retryDelta.TotalMilliseconds} мс.");
    }

    [Fact]
    public async Task ConcurrentDispatcherSignals_DoNotThrow()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient);

        var tasks = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(dispatcher.NotifyReady))
            .ToArray();

        var exception = await Record.ExceptionAsync(() => Task.WhenAll(tasks));
        Assert.Null(exception);
    }

    [Fact]
    public void LateDispatcherCallbackAfterDispose_DoesNotThrow()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        var service = CreateService(dispatcher, modbusClient);

        service.Dispose();

        var connectedException = Record.Exception(() => InvokePrivateMethod(service, "HandleDispatcherReadySignal"));
        var pingException = Record.Exception(() => InvokePrivateMethod(service, "HandlePingDataUpdated", new DiagnosticPingData()));

        Assert.Null(connectedException);
        Assert.Null(pingException);
    }

    [Fact]
    public async Task ClearAndDrainAsync_RespectsCallerCancellationWhileWaitingForQuiescence()
    {
        var dispatcher = new TestModbusDispatcher(ready: true);
        var modbusClient = new TrackingModbusClient();
        using var service = CreateService(dispatcher, modbusClient, refreshInterval: TimeSpan.FromSeconds(5));
        service.ArmMode(4, "Coms/CH_Start_ST_Heatout");
        var heldLease = await service.AcquireModeChangeLeaseAsync(CancellationToken.None);
        using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(60));

        var task = service.ClearAndDrainAsync("caller-cancel test", callerCts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        heldLease.Dispose();
    }

    private static BoilerOperationModeRefreshService CreateService(
        TestModbusDispatcher dispatcher,
        IModbusClient modbusClient,
        TimeSpan? refreshInterval = null,
        TimeSpan? dispatcherPollInterval = null,
        TimeSpan? failedRefreshRetryDelay = null,
        int writeVerifyDelayMs = -1)
    {
        var settings = CreateDiagnosticSettings(writeVerifyDelayMs);
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
            failedRefreshRetryDelay ?? RetryDelay);
    }

    private static BoilerOperationModeRefreshService CreateService(
        TestModbusDispatcher dispatcher,
        TrackingModbusClient modbusClient,
        PlcResetCoordinator plcResetCoordinator,
        RecordingErrorCoordinator errorCoordinator,
        BoilerState boilerState)
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
            RefreshInterval,
            DispatcherPollInterval,
            RetryDelay,
            plcResetCoordinator,
            errorCoordinator,
            boilerState);
    }

    private static DiagnosticSettings CreateDiagnosticSettings(
        int writeVerifyDelayMs = -1,
        TimeSpan? operationModeRefreshInterval = null)
    {
        return new DiagnosticSettings
        {
            BaseAddressOffset = 1,
            OperationModeRefreshInterval = operationModeRefreshInterval ?? TimeSpan.FromMinutes(15),
            WriteVerifyDelayMs = writeVerifyDelayMs >= 0
                ? writeVerifyDelayMs
                : (int)RetryDelay.TotalMilliseconds
        };
    }

    private static BoilerState CreateBoilerState()
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings()));
        return new BoilerState(appSettings, new TestRecipeProvider());
    }

    private static PlcResetCoordinator CreateUninitializedPlcResetCoordinator()
    {
        return (PlcResetCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(PlcResetCoordinator));
    }

    private static void RaisePlcForceStop(PlcResetCoordinator coordinator)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            typeof(PlcResetCoordinator).GetField("OnForceStop", BindingFlags.Instance | BindingFlags.NonPublic));
        var handler = Assert.IsAssignableFrom<Action>(field.GetValue(coordinator));
        handler.Invoke();
    }

    private static void InvokePrivateMethod(object target, string methodName, params object[] args)
    {
        var argumentTypes = args.Select(arg => arg.GetType()).ToArray();
        var method = Assert.IsAssignableFrom<MethodInfo>(
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: argumentTypes,
                modifiers: null));
        method.Invoke(target, args);
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

    private sealed class TestModbusDispatcher : IModbusDispatcher
    {
        private event Action? ConnectedInternal;
        private event Action<DiagnosticPingData>? PingDataUpdatedInternal;

        public int StartCalls { get; private set; }
        public bool IsConnected { get; private set; }
        public bool IsReconnecting { get; private set; }
        public bool IsStarted { get; private set; }
        public DiagnosticPingData? LastPingData { get; private set; }

        public event Func<Task>? Disconnecting
        {
            add { }
            remove { }
        }

        event Action? IModbusDispatcher.Connected
        {
            add => ConnectedInternal += value;
            remove => ConnectedInternal -= value;
        }

        public event Action? Stopped
        {
            add { }
            remove { }
        }

        event Action<DiagnosticPingData>? IModbusDispatcher.PingDataUpdated
        {
            add => PingDataUpdatedInternal += value;
            remove => PingDataUpdatedInternal -= value;
        }

        public ValueTask EnqueueAsync(IModbusCommand command, CancellationToken ct = default)
        {
            return ValueTask.CompletedTask;
        }

        public Task StartAsync(CancellationToken ct = default)
        {
            StartCalls++;
            SetReady();
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsStarted = false;
            IsConnected = false;
            IsReconnecting = false;
            LastPingData = null;
            return Task.CompletedTask;
        }

        public void SetReady()
        {
            IsStarted = true;
            IsConnected = true;
            IsReconnecting = false;
            LastPingData = new DiagnosticPingData();
            NotifyReady();
        }

        public void NotifyReady()
        {
            var pingData = LastPingData ?? new DiagnosticPingData();
            LastPingData = pingData;
            ConnectedInternal?.Invoke();
            PingDataUpdatedInternal?.Invoke(pingData);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public TestModbusDispatcher(bool ready)
        {
            if (ready)
            {
                SetReady();
                return;
            }

            IsStarted = true;
            IsConnected = false;
            IsReconnecting = true;
        }
    }

    private sealed class TrackingModbusClient : IModbusClient
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ushort, ushort> _registers = [];
        private const string DefaultWriteFailureMessage = "write failure";

        public int WriteSingleRegisterCalls { get; private set; }
        public List<ushort> WrittenValues { get; } = [];
        public List<DateTime> WriteMomentsUtc { get; } = [];
        public ushort? ForcedReadValue { get; init; }
        public HashSet<int> FailWriteCallNumbers { get; } = [];
        public Action<int>? AfterWriteCall { get; set; }

        public Task<ushort[]> ReadHoldingRegistersAsync(
            ushort address,
            ushort count,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                if (ForcedReadValue is { } forcedReadValue && count == 1)
                {
                    return Task.FromResult(new[] { forcedReadValue });
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
            int writeCallNumber;
            lock (_lock)
            {
                WriteSingleRegisterCalls++;
                writeCallNumber = WriteSingleRegisterCalls;
                WrittenValues.Add(value);
                WriteMomentsUtc.Add(DateTime.UtcNow);

                if (FailWriteCallNumbers.Contains(writeCallNumber))
                {
                    throw new InvalidOperationException(DefaultWriteFailureMessage);
                }

                _registers[address] = value;
            }

            AfterWriteCall?.Invoke(writeCallNumber);
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

    private sealed class BlockingRefreshModbusClient : IModbusClient
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ushort, ushort> _registers = [];
        private readonly TaskCompletionSource _writeStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseWrite =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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
                var values = new ushort[count];
                for (var i = 0; i < count; i++)
                {
                    values[i] = _registers.GetValueOrDefault((ushort)(address + i));
                }

                return Task.FromResult(values);
            }
        }

        public async Task WriteSingleRegisterAsync(
            ushort address,
            ushort value,
            CommandPriority priority = CommandPriority.High,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                WriteSingleRegisterCalls++;
                WrittenValues.Add(value);
            }

            _writeStarted.TrySetResult();
            await _releaseWrite.Task.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None);

            lock (_lock)
            {
                _registers[address] = value;
            }
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

        public async Task WaitForWriteStartedAsync(TimeSpan timeout)
        {
            await _writeStarted.Task.WaitAsync(timeout);
        }

        public void ReleaseWrite()
        {
            _releaseWrite.TrySetResult();
        }

        public ValueTask DisposeAsync()
        {
            ReleaseWrite();
            return ValueTask.CompletedTask;
        }
    }

    private static Task GetBackgroundDrainTask(BoilerOperationModeRefreshService service)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            typeof(BoilerOperationModeRefreshService).GetField(
                "_backgroundDrainTask",
                BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsAssignableFrom<Task>(field.GetValue(service));
    }

    private sealed class RecordingErrorCoordinator : IErrorCoordinator
    {
        public event Action? OnReset;

        public event Action? OnRecovered
        {
            add { }
            remove { }
        }

        public event Action? OnInterruptChanged
        {
            add { }
            remove { }
        }

        public InterruptReason? CurrentInterrupt => null;

        public void RaiseReset()
        {
            OnReset?.Invoke();
        }

        public Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public void Reset()
        {
            RaiseReset();
        }

        public void ForceStop()
        {
        }

        public Task<ErrorResolution> WaitForResolutionAsync(
            WaitForResolutionOptions? options = null,
            CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task SendAskRepeatAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public Task WaitForRetrySignalResetAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class TestRecipeProvider : IRecipeProvider
    {
        public RecipeResponseDto? GetByAddress(string address)
        {
            return null;
        }

        public IReadOnlyList<RecipeResponseDto> GetAll()
        {
            return [];
        }

        public void SetRecipes(IReadOnlyList<RecipeResponseDto> recipes)
        {
        }

        public void Clear()
        {
        }

        public T? GetValue<T>(string address) where T : struct
        {
            return null;
        }

        public string? GetStringValue(string address)
        {
            return null;
        }
    }
}
