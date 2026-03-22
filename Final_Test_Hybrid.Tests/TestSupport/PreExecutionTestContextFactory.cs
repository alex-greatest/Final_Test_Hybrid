using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Results;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Common.Settings;
using Final_Test_Hybrid.Services.Export;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.Main.PlcReset;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.SpringBoot.Operation.Interrupt;
using Final_Test_Hybrid.Services.SpringBoot.Operator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Services.Steps.Steps.Misc;
using Final_Test_Hybrid.Settings.App;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Final_Test_Hybrid.Tests.TestSupport;

internal static class PreExecutionTestContextFactory
{
    public static PreExecutionTestContext Create(ILoggerFactory? loggerFactory = null)
    {
        loggerFactory ??= LoggerFactory.Create(_ => { });
        var appSettings = new AppSettingsService(Options.Create(new AppSettings()));
        var recipeProvider = new RecipeProvider(
            loggerFactory.CreateLogger<RecipeProvider>(),
            new TestStepLoggerStub());
        var boilerState = new BoilerState(appSettings, recipeProvider);
        var stepHistory = new StepHistoryService();
        var exporter = new StepHistoryExcelExporter(
            appSettings,
            boilerState,
            new DualLogger<StepHistoryExcelExporter>(
                loggerFactory.CreateLogger<StepHistoryExcelExporter>(),
                new TestStepLoggerStub()));
        var sequenceService = new TestSequenseService(stepHistory, exporter, boilerState);
        var scanStep = CreateUninitialized<ScanBarcodeStep>();
        var scanBarcodeMesStep = CreateUninitialized<ScanBarcodeMesStep>();
        var statusReporter = new StepStatusReporter(sequenceService, appSettings, scanStep, scanBarcodeMesStep);
        var connectionState = new OpcUaConnectionState(loggerFactory.CreateLogger<OpcUaConnectionState>());
        var stepTimingService = new StepTimingService();
        var timerService = new TestTimerServiceStub();
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            loggerFactory.CreateLogger<AutoReadySubscription>());
        var testCoordinator = CreateTestCoordinator();
        var errorCoordinator = new StubErrorCoordinator();
        var plcResetCoordinator = CreateUninitialized<PlcResetCoordinator>();
        var changeoverStartGate = new StubChangeoverStartGate();
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
            new TestErrorService(),
            new TestResultsServiceStub(),
            recipeProvider,
            stepHistory,
            timerService,
            new DualLogger<PreExecutionCoordinator>(
                loggerFactory.CreateLogger<PreExecutionCoordinator>(),
                new TestStepLoggerStub()),
            appSettings,
            CreateUninitialized<InterruptReasonRouter>());
        var coordinators = new PreExecutionCoordinators(
            testCoordinator,
            errorCoordinator,
            plcResetCoordinator,
            CreateUninitialized<ScanDialogCoordinator>(),
            changeoverStartGate,
            CreateUninitialized<TestCompletionCoordinator>(),
            CreateUninitialized<TestCompletionUiState>());
        var state = new PreExecutionState(
            boilerState,
            new OperatorState(),
            new ExecutionActivityTracker(),
            new ExecutionPhaseState(),
            new ExecutionFlowState());
        var runtimeTerminalState = new RuntimeTerminalState(
            new DualLogger<RuntimeTerminalState>(
                loggerFactory.CreateLogger<RuntimeTerminalState>(),
                new TestStepLoggerStub()));
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
            runtimeTerminalState);

        return new PreExecutionTestContext(
            coordinator,
            boilerState,
            stepTimingService);
    }

    private static TestExecutionCoordinator CreateTestCoordinator()
    {
        var coordinator = CreateUninitialized<TestExecutionCoordinator>();
        SetBackingField(coordinator, "<StateManager>k__BackingField", new ExecutionStateManager());
        return coordinator;
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static void SetBackingField(object instance, string name, object? value)
    {
        var field = typeof(TestExecutionCoordinator).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(instance, value);
    }

    internal sealed class StubErrorCoordinator : IErrorCoordinator
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

    internal sealed class StubChangeoverStartGate : IChangeoverStartGate
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
}

internal sealed record PreExecutionTestContext(
    PreExecutionCoordinator Coordinator,
    BoilerState BoilerState,
    StepTimingService StepTimingService);
