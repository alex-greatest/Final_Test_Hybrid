using System.Reflection;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Components.Main;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Export;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Scanner;
using Final_Test_Hybrid.Services.Scanner.RawInput;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Tests.TestSupport;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class BoilerInfoInputDraftTests
{
    [Fact]
    public void SyncFromPreserved_FillsDraft_WhenPreservedValueAppears()
    {
        var draft = new BoilerInfoInputDraft();

        draft.SyncFromPreserved("SN-001");

        Assert.Equal("SN-001", draft.Draft);
    }

    [Fact]
    public void SyncFromPreserved_DoesNotOverwriteManualEdit_WhenPreservedValueDidNotChange()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update("SN-001X");
        draft.SyncFromPreserved("SN-001");

        Assert.Equal("SN-001X", draft.Draft);
    }

    [Fact]
    public void GetSubmitValue_DoesNotFallbackToOldPreservedValue_AfterManualClear()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update(string.Empty);
        draft.SyncFromPreserved("SN-001");

        Assert.Equal(string.Empty, draft.Draft);
        Assert.Null(draft.GetSubmitValue());
    }

    [Fact]
    public void SyncFromPreserved_UpdatesDraft_WhenNewPreservedValueArrives()
    {
        var draft = new BoilerInfoInputDraft();
        draft.SyncFromPreserved("SN-001");

        draft.Update("temporary-edit");
        draft.SyncFromPreserved("SN-002");

        Assert.Equal("SN-002", draft.Draft);
    }

    [Fact]
    public void IsFieldReadOnly_StaysLocked_WhenBoilerLockInterruptIsActive()
    {
        using var loggerFactory = LoggerFactory.Create(_ => { });
        var context = PreExecutionTestContextFactory.Create(loggerFactory);
        var connectionState = new OpcUaConnectionState(loggerFactory.CreateLogger<OpcUaConnectionState>());
        var autoReady = TestInfrastructure.CreateAutoReadySubscription(
            connectionState: connectionState,
            loggerFactory: loggerFactory);
        var scannerOwnership = CreateOwnershipService(loggerFactory);
        var controller = CreateReadyScanModeController(
            scannerOwnership,
            autoReady,
            connectionState,
            context.Coordinator,
            context.StepTimingService,
            loggerFactory);
        var sequenceService = CreateSequenceService(context.BoilerState, loggerFactory);
        var errorCoordinator = CreateErrorCoordinator();
        var plcResetCoordinator = CreatePlcResetCoordinator();

        try
        {
            sequenceService.EnsureScanStepExists("Сканирование", "Ожидание штрихкода");
            scannerOwnership.EnsurePreExecutionOwner(_ => { });
            SetAcceptingInput(context.Coordinator, true);

            var component = new BoilerInfo();
            SetInjectedMember(component, "PreExecution", context.Coordinator);
            SetInjectedMember(component, "ScanModeController", controller);
            SetInjectedMember(component, "BoilerState", context.BoilerState);
            SetInjectedMember(component, "TestSequenseService", sequenceService);
            SetInjectedMember(component, "ErrorCoordinator", errorCoordinator);
            SetInjectedMember(component, "PlcResetCoordinator", plcResetCoordinator);
            SetInjectedMember(component, "ScannerInputOwnership", scannerOwnership);
            SetInjectedMember(component, "OpcUaConnectionState", connectionState);

            Assert.False(GetIsFieldReadOnly(component));

            SetCurrentInterrupt(errorCoordinator, InterruptReason.BoilerLock);

            Assert.True(GetIsFieldReadOnly(component));
        }
        finally
        {
            controller.Dispose();
            scannerOwnership.Dispose();
            context.BoilerState.StopChangeoverTimer();
            context.StepTimingService.Dispose();
        }
    }

    private static bool GetIsFieldReadOnly(BoilerInfo component)
    {
        var property = typeof(BoilerInfo).GetProperty("IsFieldReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<bool>(property.GetValue(component));
    }

    private static void SetInjectedMember(object instance, string name, object? value)
    {
        var property = instance.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (property != null)
        {
            property.SetValue(instance, value);
            return;
        }

        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    private static void SetCurrentInterrupt(ErrorCoordinator coordinator, InterruptReason? reason)
    {
        TestInfrastructure.SetPrivateField(coordinator, "<CurrentInterrupt>k__BackingField", reason);
    }

    private static void SetAcceptingInput(object coordinator, bool value)
    {
        TestInfrastructure.SetPrivateField(coordinator, "<IsAcceptingInput>k__BackingField", value);
    }

    private static ErrorCoordinator CreateErrorCoordinator()
    {
        var coordinator = (ErrorCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(ErrorCoordinator));
        SetCurrentInterrupt(coordinator, null);
        return coordinator;
    }

    private static PlcResetCoordinator CreatePlcResetCoordinator()
    {
        var coordinator = (PlcResetCoordinator)RuntimeHelpers.GetUninitializedObject(typeof(PlcResetCoordinator));
        TestInfrastructure.SetPrivateField(coordinator, "<IsActive>k__BackingField", false);
        return coordinator;
    }

    private static TestSequenseService CreateSequenceService(BoilerState boilerState, ILoggerFactory loggerFactory)
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings()));
        var stepHistory = new StepHistoryService();
        var exporter = new StepHistoryExcelExporter(
            appSettings,
            boilerState,
            new DualLogger<StepHistoryExcelExporter>(
                loggerFactory.CreateLogger<StepHistoryExcelExporter>(),
                new TestStepLoggerStub()));
        return new TestSequenseService(stepHistory, exporter, boilerState);
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

    private static ScanModeController CreateReadyScanModeController(
        ScannerInputOwnershipService scannerOwnership,
        AutoReadySubscription autoReady,
        OpcUaConnectionState connectionState,
        PreExecutionCoordinator coordinator,
        IStepTimingService stepTimingService,
        ILoggerFactory loggerFactory)
    {
        var operatorState = new OperatorState();
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
            CreatePlcResetCoordinator(),
            scannerOwnership,
            stepTimingService,
            new ExecutionActivityTracker(),
            new DualLogger<ScanModeController>(
                loggerFactory.CreateLogger<ScanModeController>(),
                new TestStepLoggerStub()));

        operatorState.SetManualAuth("tester");
        connectionState.SetConnected(true, "test");
        TestInfrastructure.SetPrivateField(autoReady, "_isReady", true);
        TestInfrastructure.SetPrivateField(controller, "_isActivated", true);
        TestInfrastructure.SetPrivateField(controller, "_isResetting", false);
        TestInfrastructure.SetPrivateField(controller, "_resetReadyTransitionPending", false);
        TestInfrastructure.SetPrivateField(controller, "_cachedIsAutoReady", true);
        return controller;
    }
}
