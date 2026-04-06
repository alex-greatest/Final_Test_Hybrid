using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Microsoft.Extensions.Configuration;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PreExecutionHardResetScannerTests
{
    [Fact]
    public async Task StartMainLoopAsync_RearmsBarcodeWait_AfterNonPlcHardReset()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        using var loopCts = new CancellationTokenSource();

        try
        {
            var loopTask = context.Coordinator.StartMainLoopAsync(loopCts.Token);
            var firstWait = await WaitForBarcodeSourceAsync(context.Coordinator);

            TestInfrastructure.InvokePrivate(context.Coordinator, "HandleHardReset");
            await WaitUntilAsync(() => firstWait.Task.IsCanceled, TimeSpan.FromSeconds(2));

            var secondWait = await WaitForBarcodeSourceAsync(context.Coordinator, firstWait);

            Assert.NotSame(firstWait, secondWait);
            Assert.True(context.Coordinator.IsAcceptingInput);
            Assert.Contains(
                logs.Entries,
                entry => entry.Level == LogLevel.Debug
                    && entry.Message.Contains("non_plc_hard_reset_cancel_barcode_wait", StringComparison.Ordinal)
                    && entry.Message.Contains("barcodeWaitActive=True", StringComparison.Ordinal));

            await loopCts.CancelAsync();
            await loopTask;
        }
        finally
        {
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandleHardReset_WhenPostAskEndActive_PublishesTransitionToReadyDecision()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var postAskEndCts = new CancellationTokenSource();

        try
        {
            TestInfrastructure.SetPrivateField(context.Coordinator, "_postAskEndCts", postAskEndCts);
            TestInfrastructure.SetPrivateField(context.Coordinator, "_postAskEndActive", 1);

            TestInfrastructure.InvokePrivate(context.Coordinator, "HandleHardReset");

            var consumed = TryConsumePostAskEndScanModeDecision(context.Coordinator);

            Assert.True(consumed.HasDecision);
            Assert.True(consumed.ShouldTransitionToReady);
            Assert.Equal(0, TestInfrastructure.GetPrivateField<int>(context.Coordinator, "_postAskEndActive"));
        }
        finally
        {
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void TryCompleteDeferredResetTransitionUnsafe_RearmsScannerOwner_AfterHardResetAbortDecision()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var operatorState = new OperatorState();
        var connectionState = new OpcUaConnectionState(loggerFactory.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            new DualLogger<OpcUaSubscription>(
                loggerFactory.CreateLogger<OpcUaSubscription>(),
                new TestStepLoggerStub()));
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            TestInfrastructure.CreateHeartbeatHealthMonitor(),
            TestInfrastructure.CreateDualLogger<AutoReadySubscription>(loggerFactory));
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var sessionManager = new ScanSessionManager(
            scannerOwnership,
            loggerFactory.CreateLogger<ScanSessionManager>());
        var controller = CreateController(
            sessionManager,
            scannerOwnership,
            operatorState,
            autoReady,
            connectionState,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);

        try
        {
            operatorState.SetManualAuth("tester");
            connectionState.SetConnected(true, "test");
            TestInfrastructure.SetPrivateField(autoReady, "_isReady", true);
            TestInfrastructure.SetPrivateField(context.Coordinator, "_postAskEndScanModeDecision", 1);

            var transitioned = Assert.IsType<bool>(
                TestInfrastructure.InvokePrivate(controller, "TryCompleteDeferredResetTransitionUnsafe"));

            Assert.True(transitioned);
            Assert.True(scannerOwnership.IsPreExecutionOwnerActive);
            Assert.False(TestInfrastructure.GetPrivateField<bool>(controller, "_resetReadyTransitionPending"));
            Assert.False(TestInfrastructure.GetPrivateField<bool>(controller, "_isResetting"));
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandlePreExecutionStateChanged_RearmsScannerOwner_WhenBarcodeWaitReturnsAfterRepeat()
    {
        using var loggerFactory = CreateLoggerFactory(out var logs);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateSelfHealController(
            scannerOwnership,
            autoReady,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);

        try
        {
            SetAcceptingInput(context.Coordinator, true);

            TestInfrastructure.InvokePrivate(controller, "HandlePreExecutionStateChanged");

            Assert.True(scannerOwnership.IsPreExecutionOwnerActive);
            Assert.Contains(
                logs.Entries,
                entry => entry.Level == LogLevel.Information
                    && entry.Message.Contains(
                        "Обычный scanner owner перевооружён при возврате в ожидание barcode",
                        StringComparison.Ordinal));
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandlePreExecutionStateChanged_DoesNotRearmScannerOwner_WhenDialogOwnerIsActive()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateSelfHealController(
            scannerOwnership,
            autoReady,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);

        try
        {
            scannerOwnership.AcquireDialogOwner("dialog", _ => { });
            SetAcceptingInput(context.Coordinator, true);

            TestInfrastructure.InvokePrivate(controller, "HandlePreExecutionStateChanged");

            var ownerState = scannerOwnership.GetCurrentOwnerState();
            Assert.Equal(ScannerInputOwnerKind.Dialog, ownerState.CurrentOwner);
            Assert.False(scannerOwnership.IsPreExecutionOwnerActive);
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandlePreExecutionStateChanged_DoesNotRearmScannerOwner_WhenAutoReadyIsFalse()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateSelfHealController(
            scannerOwnership,
            autoReady,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);

        try
        {
            SetAcceptingInput(context.Coordinator, true);
            TestInfrastructure.SetPrivateField(autoReady, "_isReady", false);
            TestInfrastructure.SetPrivateField(controller, "_cachedIsAutoReady", false);

            TestInfrastructure.InvokePrivate(controller, "HandlePreExecutionStateChanged");

            Assert.Equal(ScannerInputOwnerKind.None, scannerOwnership.GetCurrentOwnerState().CurrentOwner);
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandlePreExecutionStateChanged_DoesNotRearmScannerOwner_WhenPlcIsDisconnected()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateSelfHealController(
            scannerOwnership,
            autoReady,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory,
            isConnected: false);

        try
        {
            SetAcceptingInput(context.Coordinator, true);

            TestInfrastructure.InvokePrivate(controller, "HandlePreExecutionStateChanged");

            Assert.Equal(ScannerInputOwnerKind.None, scannerOwnership.GetCurrentOwnerState().CurrentOwner);
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    [Fact]
    public void HandlePreExecutionStateChanged_DoesNotRearmScannerOwner_WhenNotAcceptingInput()
    {
        using var loggerFactory = CreateLoggerFactory(out _);
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateSelfHealController(
            scannerOwnership,
            autoReady,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);

        try
        {
            SetAcceptingInput(context.Coordinator, false);

            TestInfrastructure.InvokePrivate(controller, "HandlePreExecutionStateChanged");

            Assert.Equal(ScannerInputOwnerKind.None, scannerOwnership.GetCurrentOwnerState().CurrentOwner);
        }
        finally
        {
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
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

    private static async Task<TaskCompletionSource<string>> WaitForBarcodeSourceAsync(
        PreExecutionCoordinator coordinator,
        TaskCompletionSource<string>? previous = null)
    {
        return await WaitUntilAsync(
            () =>
            {
                var source = GetBarcodeSource(coordinator);
                if (source == null || ReferenceEquals(source, previous) || source.Task.IsCompleted)
                {
                    return null;
                }

                return coordinator.IsAcceptingInput ? source : null;
            },
            TimeSpan.FromSeconds(2));
    }

    private static TaskCompletionSource<string>? GetBarcodeSource(PreExecutionCoordinator coordinator)
    {
        return TestInfrastructure.GetPrivateField<TaskCompletionSource<string>?>(coordinator, "_barcodeSource");
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Условие теста не выполнилось вовремя.");
    }

    private static async Task<T> WaitUntilAsync<T>(Func<T?> probe, TimeSpan timeout) where T : class
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var value = probe();
            if (value != null)
            {
                return value;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException("Условие теста не выполнилось вовремя.");
    }

    private static (bool HasDecision, bool ShouldTransitionToReady) TryConsumePostAskEndScanModeDecision(
        PreExecutionCoordinator coordinator)
    {
        var method = typeof(PreExecutionCoordinator).GetMethod(
            "TryConsumePostAskEndScanModeDecision",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        object?[] args = [false];

        var hasDecision = Assert.IsType<bool>(method.Invoke(coordinator, args));
        var shouldTransitionToReady = Assert.IsType<bool>(args[0]);
        return (hasDecision, shouldTransitionToReady);
    }

    private static ScannerInputOwnershipService CreateOwnershipService(ILoggerFactory loggerFactory)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scanner:VendorId"] = "1FBB",
                ["Scanner:ProductId"] = "3681"
            })
            .Build();
        var connectionState = (ScannerConnectionState)RuntimeHelpers.GetUninitializedObject(typeof(ScannerConnectionState));
        var detector = new ScannerDeviceDetector(configuration, loggerFactory.CreateLogger<ScannerDeviceDetector>());
        var rawInputService = new RawInputService(
            loggerFactory.CreateLogger<RawInputService>(),
            connectionState,
            detector);
        return new ScannerInputOwnershipService(
            rawInputService,
            loggerFactory.CreateLogger<ScannerInputOwnershipService>());
    }

    private static ScanModeController CreateController(
        ScanSessionManager sessionManager,
        ScannerInputOwnershipService scannerOwnership,
        OperatorState operatorState,
        AutoReadySubscription autoReady,
        OpcUaConnectionState connectionState,
        PreExecutionCoordinator coordinator,
        IStepTimingService stepTimingService,
        ILoggerFactory loggerFactory)
    {
        var controller = new ScanModeController(
            sessionManager,
            operatorState,
            autoReady,
            connectionState,
            (StepStatusReporter)RuntimeHelpers.GetUninitializedObject(typeof(StepStatusReporter)),
            (BarcodeDebounceHandler)RuntimeHelpers.GetUninitializedObject(typeof(BarcodeDebounceHandler)),
            coordinator,
            (PlcResetCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(PlcResetCoordinator)),
            scannerOwnership,
            stepTimingService,
            new ExecutionActivityTracker(),
            new DualLogger<ScanModeController>(
                loggerFactory.CreateLogger<ScanModeController>(),
                new TestStepLoggerStub()));
        TestInfrastructure.SetPrivateField(controller, "_isActivated", true);
        TestInfrastructure.SetPrivateField(controller, "_isResetting", true);
        TestInfrastructure.SetPrivateField(controller, "_resetReadyTransitionPending", true);
        TestInfrastructure.SetPrivateField(controller, "_cachedIsAutoReady", true);
        return controller;
    }

    private static ScanModeController CreateSelfHealController(
        ScannerInputOwnershipService scannerOwnership,
        AutoReadySubscription autoReady,
        PreExecutionCoordinator coordinator,
        IStepTimingService stepTimingService,
        ILoggerFactory loggerFactory,
        bool isConnected = true)
    {
        var operatorState = new OperatorState();
        var connectionState = new OpcUaConnectionState(loggerFactory.CreateLogger<OpcUaConnectionState>());
        var sessionManager = new ScanSessionManager(
            scannerOwnership,
            loggerFactory.CreateLogger<ScanSessionManager>());
        var controller = new ScanModeController(
            sessionManager,
            operatorState,
            autoReady,
            connectionState,
            (StepStatusReporter)RuntimeHelpers.GetUninitializedObject(typeof(StepStatusReporter)),
            (BarcodeDebounceHandler)RuntimeHelpers.GetUninitializedObject(typeof(BarcodeDebounceHandler)),
            coordinator,
            (PlcResetCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(PlcResetCoordinator)),
            scannerOwnership,
            stepTimingService,
            new ExecutionActivityTracker(),
            new DualLogger<ScanModeController>(
                loggerFactory.CreateLogger<ScanModeController>(),
                new TestStepLoggerStub()));

        operatorState.SetManualAuth("tester");
        connectionState.SetConnected(isConnected, "test");
        TestInfrastructure.SetPrivateField(autoReady, "_isReady", true);
        TestInfrastructure.SetPrivateField(controller, "_isActivated", true);
        TestInfrastructure.SetPrivateField(controller, "_isResetting", false);
        TestInfrastructure.SetPrivateField(controller, "_resetReadyTransitionPending", false);
        TestInfrastructure.SetPrivateField(controller, "_cachedIsAutoReady", true);
        return controller;
    }

    private static void SetAcceptingInput(PreExecutionCoordinator coordinator, bool value)
    {
        TestInfrastructure.SetPrivateField(coordinator, "<IsAcceptingInput>k__BackingField", value);
    }
}
