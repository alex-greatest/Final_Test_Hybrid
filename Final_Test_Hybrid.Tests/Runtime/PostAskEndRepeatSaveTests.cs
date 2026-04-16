using System.Collections.Concurrent;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using FinalTestResultsSnapshotBuilder = Final_Test_Hybrid.Services.Storage.FinalTestResultsSnapshotBuilder;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Plc.Tags;
using Final_Test_Hybrid.Models.Results;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Export;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.Diagnostic.Services;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Heartbeat;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Preparation;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.SpringBoot.Shift;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Steps.Misc;
using Final_Test_Hybrid.Settings.App;
using Final_Test_Hybrid.Settings.Spring;
using Final_Test_Hybrid.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PostAskEndRepeatSaveTests
{
    [Fact]
    public async Task SaveInterruptReasonBeforeRepeatAsync_UsesDialogWithRepeatBypassCancel()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var captured = new RepeatSaveDialogRequest();

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            serialNumber,
            _,
            useMes,
            requireAdminAuth,
            operatorUsername,
            showCancelButton,
            allowRepeatBypassOnCancel,
            _) =>
        {
            captured.SerialNumber = serialNumber;
            captured.UseMes = useMes;
            captured.RequireAdminAuth = requireAdminAuth;
            captured.OperatorUsername = operatorUsername;
            captured.ShowCancelButton = showCancelButton;
            captured.AllowRepeatBypassOnCancel = allowRepeatBypassOnCancel;
            return Task.FromResult(InterruptFlowResult.Success("admin-user"));
        };

        await InvokeSaveInterruptReasonBeforeRepeatAsync(
            context.Coordinator,
            "SN-42",
            expectedSequence: 7,
            CancellationToken.None);

        Assert.Equal("SN-42", captured.SerialNumber);
        Assert.True(captured.UseMes);
        Assert.True(captured.RequireAdminAuth);
        Assert.Equal("operator-user", captured.OperatorUsername);
        Assert.True(captured.ShowCancelButton);
        Assert.True(captured.AllowRepeatBypassOnCancel);
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_DoesNotAcknowledgeRepeat_WhenSaveBeforeRepeatFails()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) => Task.FromResult(InterruptFlowResult.Cancelled());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window));

        Assert.Equal(0, context.ErrorCoordinator.SendAskRepeatCalls);
        Assert.True(context.BoilerState.IsTestRunning);
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_DoesNotAcknowledgeRepeat_WhenExternalResetCancelsRepeatSave()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);
        var dialogStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += async (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            ct) =>
        {
            dialogStarted.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return InterruptFlowResult.Success("admin-user");
        };

        var handleTask = InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window);
        await dialogStarted.Task;
        InvokeCancelPostAskEndFlow(context.Coordinator);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handleTask);

        Assert.Equal(0, context.ErrorCoordinator.SendAskRepeatCalls);
        Assert.True(context.BoilerState.IsTestRunning);
    }

    [Fact]
    public async Task CreateRepeatSaveCallback_SendsInterruptBeforeStartingNewOperation()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new RequestScriptHandler(
        [
            CreateJsonResponse(HttpStatusCode.OK, "{}"),
            CreateJsonResponse(HttpStatusCode.OK, CreateStartSuccessPayload("SN-42"))
        ]);
        var context = CreateContext(loggerFactory, handler);
        var callback = CreateRepeatSaveCallback(context.Coordinator, "SN-42");

        var result = await callback("admin-user", "reason-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["/api/operation/interrupt", "/api/operation/start"],
            handler.RequestPaths);
    }

    [Fact]
    public async Task CreateRepeatSaveCallback_ReusesSavedInterruptAcrossNextRetryFlow()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new RequestScriptHandler(
        [
            CreateJsonResponse(HttpStatusCode.OK, "{}"),
            CreateJsonResponse(HttpStatusCode.InternalServerError, """
            {
              "error": "start failed"
            }
            """),
            CreateJsonResponse(HttpStatusCode.OK, CreateStartSuccessPayload("SN-42"))
        ]);
        var context = CreateContext(loggerFactory, handler);
        var firstCallback = CreateRepeatSaveCallback(context.Coordinator, "SN-42");
        var secondCallback = CreateRepeatSaveCallback(context.Coordinator, "SN-42");

        var firstResult = await firstCallback("admin-user", "reason-1", CancellationToken.None);
        var secondResult = await secondCallback("admin-user", "reason-1", CancellationToken.None);

        Assert.False(firstResult.IsSuccess);
        Assert.Equal("start failed", firstResult.ErrorMessage);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(
            ["/api/operation/interrupt", "/api/operation/start", "/api/operation/start"],
            handler.RequestPaths);
    }

    [Fact]
    public async Task StartRepeatOperationAsync_UsesLocalDatabaseStart_WhenMesDisabled()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var preparationFacade = new StubScanPreparationFacade();
        var context = CreateContext(
            loggerFactory,
            useMes: false,
            preparationFacade: preparationFacade);

        context.BoilerState.SetData("SN-42", "ART-1", true);

        var result = await InvokeStartRepeatOperationAsync(
            context.Coordinator,
            "SN-42",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, preparationFacade.InitializeDatabaseCalls);
    }

    [Fact]
    public async Task ShowInterruptReasonThenCleanupAsync_SkipsDialog_WhenInterruptWasAlreadySaved()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var dialogRequests = 0;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        TestInfrastructure.SetPrivateField(
            context.Coordinator,
            "_pendingRepeatOperationSerialNumber",
            "SN-42");
        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) =>
        {
            dialogRequests++;
            return Task.FromResult(InterruptFlowResult.Success("admin-user"));
        };

        await InvokeShowInterruptReasonThenCleanupAsync(
            context.Coordinator,
            "SN-42",
            expectedSequence: 7,
            CancellationToken.None);

        Assert.Equal(0, dialogRequests);
        Assert.Null(GetPendingRepeatOperationSerialNumber(context.Coordinator));
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_StartsRepeatWithoutMesRequests_WhenRepeatBypassRequested()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var handler = new RequestScriptHandler([]);
        var context = CreateContext(loggerFactory, handler);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) => Task.FromResult(InterruptFlowResult.RepeatBypass());

        await InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window);

        Assert.Equal(1, context.ErrorCoordinator.SendAskRepeatCalls);
        Assert.Empty(handler.RequestPaths);
        Assert.False(context.BoilerState.IsTestRunning);
        Assert.Null(GetPendingRepeatOperationSerialNumber(context.Coordinator));
        Assert.Equal(GetPostAskEndScanModeDecisionRepeatValue(), GetPostAskEndScanModeDecision(context.Coordinator));
        Assert.Equal(1, GetInterruptDialogCompletedInCurrentSeries(context.Coordinator));
        Assert.Equal(0, GetInterruptDialogAllowedSequence(context.Coordinator));
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_StartsRepeatWithoutLocalDbCreate_WhenRepeatBypassRequested()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var preparationFacade = new StubScanPreparationFacade();
        var context = CreateContext(
            loggerFactory,
            useMes: false,
            preparationFacade: preparationFacade);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) => Task.FromResult(InterruptFlowResult.RepeatBypass());

        await InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window);

        Assert.Equal(1, context.ErrorCoordinator.SendAskRepeatCalls);
        Assert.Equal(0, preparationFacade.InitializeDatabaseCalls);
        Assert.False(context.BoilerState.IsTestRunning);
        Assert.Null(GetPendingRepeatOperationSerialNumber(context.Coordinator));
        Assert.Equal(GetPostAskEndScanModeDecisionRepeatValue(), GetPostAskEndScanModeDecision(context.Coordinator));
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_DoesNotRearmInterruptDialogSeries_AfterRepeatBypass()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;

        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) => Task.FromResult(InterruptFlowResult.RepeatBypass());

        await InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window);

        InvokeHandleSoftStop(context.Coordinator);

        Assert.Equal(1, GetInterruptDialogCompletedInCurrentSeries(context.Coordinator));
        Assert.Equal(0, GetInterruptDialogAllowedSequence(context.Coordinator));
    }

    [Fact]
    public async Task HandlePostAskEndDecisionAsync_PreservesBoilerTimers_WhenRepeatBypassRequested()
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        var context = CreateContext(loggerFactory);
        var values = TestInfrastructure.GetSubscriptionValues(context.Subscription);
        values[BaseTags.ErrorRetry] = true;
        var expectedTestDuration = TimeSpan.FromSeconds(123);
        var expectedChangeoverDuration = TimeSpan.FromSeconds(456);

        TestInfrastructure.SetPrivateField(context.BoilerState, "_testStoppedDuration", expectedTestDuration);
        TestInfrastructure.SetPrivateField(context.BoilerState, "_changeoverStoppedDuration", expectedChangeoverDuration);
        context.BoilerState.SetData("SN-42", "ART-1", true);
        context.BoilerState.SetTestRunning(true);
        PreparePostAskEndToken(context.Coordinator);
        var window = CreateResetAskEndWindow(context.Coordinator, sequence: 7);

        context.DialogCoordinator.OnInterruptReasonDialogRequested += (
            _,
            _,
            _,
            _,
            _,
            _,
            _,
            _) => Task.FromResult(InterruptFlowResult.RepeatBypass());

        await InvokeHandlePostAskEndDecisionAsync(context.Coordinator, window);

        Assert.Equal(expectedTestDuration, context.BoilerState.GetTestDuration());
        Assert.Equal(expectedChangeoverDuration, context.BoilerState.GetChangeoverDuration());
    }

    private static async Task InvokeSaveInterruptReasonBeforeRepeatAsync(
        PreExecutionCoordinator coordinator,
        string serialNumber,
        int expectedSequence,
        CancellationToken ct)
    {
        await Assert.IsAssignableFrom<Task>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "SaveInterruptReasonBeforeRepeatAsync",
                serialNumber,
                expectedSequence,
                ct));
    }

    private static Task InvokeHandlePostAskEndDecisionAsync(
        PreExecutionCoordinator coordinator,
        object window)
    {
        return Assert.IsAssignableFrom<Task>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "HandlePostAskEndDecisionAsync",
                window));
    }

    private static async Task InvokeShowInterruptReasonThenCleanupAsync(
        PreExecutionCoordinator coordinator,
        string serialNumber,
        int expectedSequence,
        CancellationToken ct)
    {
        await Assert.IsAssignableFrom<Task>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "ShowInterruptReasonThenCleanupAsync",
                serialNumber,
                expectedSequence,
                ct));
    }

    private static async Task<SaveResult> InvokeStartRepeatOperationAsync(
        PreExecutionCoordinator coordinator,
        string serialNumber,
        CancellationToken ct)
    {
        return await Assert.IsType<Task<SaveResult>>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "StartRepeatOperationAsync",
                serialNumber,
                ct));
    }

    private static void PreparePostAskEndToken(PreExecutionCoordinator coordinator)
    {
        TestInfrastructure.SetPrivateField(
            coordinator,
            "_postAskEndCts",
            new CancellationTokenSource());
    }

    private static object CreateResetAskEndWindow(PreExecutionCoordinator coordinator, int sequence)
    {
        var windowType = Assert.IsAssignableFrom<Type>(
            coordinator.GetType().GetNestedType("ResetAskEndWindow", BindingFlags.NonPublic));
        return Assert.IsAssignableFrom<object>(
            Activator.CreateInstance(
                windowType,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                binder: null,
                args: [sequence],
                culture: null));
    }

    private static void InvokeCancelPostAskEndFlow(PreExecutionCoordinator coordinator)
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(
            coordinator.GetType().GetMethod(
                "CancelPostAskEndFlow",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [],
                modifiers: null));
        _ = method.Invoke(coordinator, null);
    }

    private static void InvokeHandleSoftStop(PreExecutionCoordinator coordinator)
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(
            coordinator.GetType().GetMethod(
                "HandleSoftStop",
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [],
                modifiers: null));
        _ = method.Invoke(coordinator, null);
    }

    private static Func<string, string, CancellationToken, Task<SaveResult>> CreateRepeatSaveCallback(
        PreExecutionCoordinator coordinator,
        string serialNumber)
    {
        return Assert.IsAssignableFrom<Func<string, string, CancellationToken, Task<SaveResult>>>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "CreateRepeatSaveCallback",
                serialNumber));
    }

    private static string? GetPendingRepeatOperationSerialNumber(PreExecutionCoordinator coordinator)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            coordinator.GetType().GetField(
                "_pendingRepeatOperationSerialNumber",
                BindingFlags.Instance | BindingFlags.NonPublic));
        return field.GetValue(coordinator) as string;
    }

    private static int GetInterruptDialogCompletedInCurrentSeries(PreExecutionCoordinator coordinator)
    {
        return GetPrivateIntField(coordinator, "_interruptDialogCompletedInCurrentResetSeries");
    }

    private static int GetInterruptDialogAllowedSequence(PreExecutionCoordinator coordinator)
    {
        return GetPrivateIntField(coordinator, "_interruptDialogAllowedSequence");
    }

    private static int GetPostAskEndScanModeDecision(PreExecutionCoordinator coordinator)
    {
        return GetPrivateIntField(coordinator, "_postAskEndScanModeDecision");
    }

    private static int GetPostAskEndScanModeDecisionRepeatValue()
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            typeof(PreExecutionCoordinator).GetField(
                "PostAskEndScanModeDecisionRepeat",
                BindingFlags.Static | BindingFlags.NonPublic));
        return Assert.IsType<int>(field.GetRawConstantValue());
    }

    private static int GetPrivateIntField(PreExecutionCoordinator coordinator, string fieldName)
    {
        var field = Assert.IsAssignableFrom<FieldInfo>(
            coordinator.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic));
        return Assert.IsType<int>(field.GetValue(coordinator));
    }

    private static HttpResponseMessage CreateJsonResponse(HttpStatusCode statusCode, string json)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static string CreateStartSuccessPayload(string serialNumber)
    {
        return $$"""
        {
          "boilerMadeInformation": {
            "orderNumber": 1,
            "amountBoilerMadeOrder": 1,
            "amountBoilerOrder": 1
          },
          "boilerTypeCycle": {
            "typeName": "Type-A",
            "article": "ART-1",
            "serialNumber": "{{serialNumber}}",
            "model": "M1"
          },
          "recipes": [
            {
              "parameter": "P1",
              "value": "1",
              "plcType": "STRING"
            }
          ]
        }
        """;
    }

    private static TestContext CreateContext(
        ILoggerFactory loggerFactory,
        RequestScriptHandler? requestHandler = null,
        bool useMes = true,
        IScanPreparationFacade? preparationFacade = null)
    {
        var appSettings = new AppSettingsService(Options.Create(new AppSettings
        {
            NameStation = "ST-01",
            UseMes = useMes,
            UseInterruptReason = true
        }));
        var recipeProvider = new RecipeProvider(
            loggerFactory.CreateLogger<RecipeProvider>(),
            new TestStepLoggerStub());
        var boilerState = new BoilerState(appSettings, recipeProvider);
        var operatorState = new OperatorState();
        operatorState.SetManualAuth("operator-user");
        var requestPipeline = requestHandler ?? new RequestScriptHandler([]);
        var httpClient = new HttpClient(requestPipeline)
        {
            BaseAddress = new Uri("http://localhost")
        };
        var springHttpClient = new SpringBootHttpClient(
            httpClient,
            loggerFactory.CreateLogger<SpringBootHttpClient>());

        var stepHistory = new StepHistoryService();
        var exporter = new StepHistoryExcelExporter(
            appSettings,
            boilerState,
            new DualLogger<StepHistoryExcelExporter>(
                loggerFactory.CreateLogger<StepHistoryExcelExporter>(),
                new TestStepLoggerStub()));
        var sequenceService = new TestSequenseService(stepHistory, exporter, boilerState);
        preparationFacade ??= new StubScanPreparationFacade();
        var testResultsService = new TestResultsServiceStub();
        var errorService = new TestErrorService();
        var stepTimingService = new StepTimingService();
        var snapshotBuilder = new FinalTestResultsSnapshotBuilder(
            boilerState,
            operatorState,
            appSettings,
            testResultsService,
            errorService,
            stepTimingService,
            TestInfrastructure.CreateDualLogger<FinalTestResultsSnapshotBuilder>(loggerFactory));
        var interruptReasonRouter = new InterruptReasonRouter(
            new InterruptedOperationService(
                springHttpClient,
                appSettings,
                snapshotBuilder,
                TestInfrastructure.CreateDualLogger<InterruptedOperationService>(loggerFactory)),
            CreateUninitialized<InterruptReasonStorageService>());
        var operationStartService = new OperationStartService(
            springHttpClient,
            appSettings,
            TestInfrastructure.CreateDualLogger<OperationStartService>(loggerFactory));
        var scanStep = new ScanBarcodeStep(
            null!,
            null!,
            null!,
            null!,
            null!,
            boilerState,
            null!,
            recipeProvider,
            new ExecutionPhaseState(),
            preparationFacade,
            operatorState,
            new ShiftState(),
            loggerFactory.CreateLogger<ScanBarcodeStep>(),
            new TestStepLoggerStub());
        var scanBarcodeMesStep = new ScanBarcodeMesStep(
            null!,
            null!,
            null!,
            null!,
            null!,
            boilerState,
            null!,
            recipeProvider,
            new ExecutionPhaseState(),
            operationStartService,
            operatorState,
            new ShiftState(),
            new OrderState(appSettings),
            loggerFactory.CreateLogger<ScanBarcodeMesStep>(),
            new TestStepLoggerStub());
        var statusReporter = new StepStatusReporter(
            sequenceService,
            appSettings,
            scanStep,
            scanBarcodeMesStep);
        var connectionState = new OpcUaConnectionState(
            loggerFactory.CreateLogger<OpcUaConnectionState>());
        var timerService = new TestTimerServiceStub();
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>(loggerFactory));
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            new HmiHeartbeatHealthMonitor(TimeProvider.System),
            TestInfrastructure.CreateDualLogger<AutoReadySubscription>(loggerFactory));
        var errorCoordinator = new StubErrorCoordinator();
        var plcResetCoordinator = CreateUninitialized<PlcResetCoordinator>();
        var dialogCoordinator = new ScanDialogCoordinator(
            CreateUninitialized<ScanErrorHandler>(),
            scanBarcodeMesStep);
        var completionUiState = new TestCompletionUiState(plcResetCoordinator, errorCoordinator);
        var coordinators = new PreExecutionCoordinators(
            CreateTestCoordinator(),
            errorCoordinator,
            plcResetCoordinator,
            dialogCoordinator,
            new StubChangeoverStartGate(),
            CreateUninitialized<TestCompletionCoordinator>(),
            completionUiState);
        var infra = new PreExecutionInfrastructure(
            null!,
            null!,
            subscription,
            connectionState,
            autoReady,
            null!,
            new PauseTokenSource(),
            stepTimingService,
            statusReporter,
            new TestStepLoggerStub(),
            errorService,
            testResultsService,
            recipeProvider,
            stepHistory,
            timerService,
            new DualLogger<PreExecutionCoordinator>(
                loggerFactory.CreateLogger<PreExecutionCoordinator>(),
                new TestStepLoggerStub()),
            appSettings,
            interruptReasonRouter);
        var state = new PreExecutionState(
            boilerState,
            operatorState,
            new ExecutionActivityTracker(),
            new ExecutionPhaseState(),
            new ExecutionFlowState());
        var coordinator = new PreExecutionCoordinator(
            new PreExecutionSteps(
                appSettings,
                scanStep,
                scanBarcodeMesStep,
                new StartTimer1Step(
                    timerService,
                    new DualLogger<StartTimer1Step>(
                        loggerFactory.CreateLogger<StartTimer1Step>(),
                        new TestStepLoggerStub())),
                CreateUninitialized<BlockBoilerAdapterStep>()),
            infra,
            coordinators,
            state,
            new RuntimeTerminalState(
                new DualLogger<RuntimeTerminalState>(
                    loggerFactory.CreateLogger<RuntimeTerminalState>(),
                    new TestStepLoggerStub())));

        return new TestContext(
            coordinator,
            dialogCoordinator,
            errorCoordinator,
            boilerState,
            subscription);
    }

    private static TestExecutionCoordinator CreateTestCoordinator()
    {
        var coordinator = CreateUninitialized<TestExecutionCoordinator>();
        var modeRefreshService = CreateUninitialized<BoilerOperationModeRefreshService>();
        var stateLock = Activator.CreateInstance(typeof(Lock))!;
        TestInfrastructure.SetPrivateField(modeRefreshService, "_stateLock", stateLock);
        TestInfrastructure.SetPrivateField(modeRefreshService, "_disposed", true);

        TestInfrastructure.SetPrivateField(
            coordinator,
            "<StateManager>k__BackingField",
            new ExecutionStateManager());
        TestInfrastructure.SetPrivateField(
            coordinator,
            "_boilerOperationModeRefreshService",
            modeRefreshService);
        TestInfrastructure.SetPrivateField(
            coordinator,
            "_executors",
            Array.Empty<ColumnExecutor>());
        return coordinator;
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private sealed record TestContext(
        PreExecutionCoordinator Coordinator,
        ScanDialogCoordinator DialogCoordinator,
        StubErrorCoordinator ErrorCoordinator,
        BoilerState BoilerState,
        OpcUaSubscription Subscription);

    private sealed class RepeatSaveDialogRequest
    {
        public string SerialNumber { get; set; } = string.Empty;
        public bool UseMes { get; set; }
        public bool RequireAdminAuth { get; set; }
        public string OperatorUsername { get; set; } = string.Empty;
        public bool ShowCancelButton { get; set; }
        public bool AllowRepeatBypassOnCancel { get; set; }
    }

    private sealed class StubErrorCoordinator : IErrorCoordinator
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

        public Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public void Reset()
        {
            OnReset?.Invoke();
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
            SendAskRepeatCalls++;
            return Task.CompletedTask;
        }

        public Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
        {
            SendAskRepeatCalls++;
            return Task.CompletedTask;
        }

        public Task WaitForRetrySignalResetAsync(CancellationToken ct)
        {
            throw new NotSupportedException();
        }

        public int SendAskRepeatCalls { get; private set; }
    }

    private sealed class StubChangeoverStartGate : IChangeoverStartGate
    {
        public event Action? OnAutoReadyRequested;

        public void RequestStartFromAutoReady()
        {
            OnAutoReadyRequested?.Invoke();
        }

        public bool TryConsumePendingAutoReadyRequest()
        {
            return false;
        }
    }

    private sealed class TestResultsServiceStub : ITestResultsService
    {
        public event Action? OnChanged;

        public IReadOnlyList<TestResultItem> GetResults()
        {
            return [];
        }

        public void Add(string parameterName, string value, string min, string max, int status, bool isRanged, string unit, string test)
        {
        }

        public void Remove(string parameterName)
        {
        }

        public void Clear()
        {
            OnChanged?.Invoke();
        }
    }

    private sealed class TestTimerServiceStub : ITimerService
    {
        public event Action? OnChanged;

        public void Start(string key)
        {
        }

        public TimeSpan? Stop(string key)
        {
            return null;
        }

        public TimeSpan? GetElapsed(string key)
        {
            return null;
        }

        public bool IsRunning(string key)
        {
            return false;
        }

        public IReadOnlyDictionary<string, TimeSpan> GetAllActive()
        {
            return new ConcurrentDictionary<string, TimeSpan>();
        }

        public void StopAll()
        {
        }

        public void Clear()
        {
            OnChanged?.Invoke();
        }
    }

    private sealed class RequestScriptHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestPaths { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestPaths.Add(request.RequestUri?.AbsolutePath ?? string.Empty);
            if (request.Content != null)
            {
                _ = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_responses.Count == 0)
            {
                throw new InvalidOperationException("Для теста не настроен ответ HTTP");
            }

            return _responses.Dequeue();
        }
    }

    private sealed class StubScanPreparationFacade : IScanPreparationFacade
    {
        public int InitializeDatabaseCalls { get; private set; }

        public Task<PreExecutionResult?> LoadBoilerDataAsync(PreExecutionContext context)
        {
            return Task.FromResult<PreExecutionResult?>(null);
        }

        public Task<PreExecutionResult?> InitializeDatabaseAsync(
            BoilerState boilerState,
            string operatorName,
            int shiftNumber)
        {
            InitializeDatabaseCalls++;
            return Task.FromResult<PreExecutionResult?>(null);
        }
    }
}
