using Final_Test_Hybrid.Models;
using Final_Test_Hybrid.Models.Steps;
using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.ErrorCoordinator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PreExecutionRetryHandshakeTests
{
    [Fact]
    public async Task PrepareRetryStartAsync_WaitsForReqRepeatReset_BeforeRetry()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var coordinator = CreateCoordinator(subscription, out var errorCoordinator);
        var step = CreateStep(subscription);

        await TestInfrastructure.InvokePrivateAsync(
            coordinator,
            "PrepareRetryStartAsync",
            step,
            CancellationToken.None);

        Assert.Equal(["SendAskRepeat", "WaitForRetrySignalReset"], errorCoordinator.Calls);
    }

    [Fact]
    public async Task PrepareRetryStartAsync_PropagatesReqRepeatResetTimeout()
    {
        var subscription = TestInfrastructure.CreateSubscription();
        var coordinator = CreateCoordinator(subscription, out var errorCoordinator);
        var step = CreateStep(subscription);
        errorCoordinator.ThrowOnWaitForRetrySignalReset = true;

        await Assert.ThrowsAsync<TimeoutException>(() =>
            TestInfrastructure.InvokePrivateAsync(
                coordinator,
                "PrepareRetryStartAsync",
                step,
                CancellationToken.None));

        Assert.Equal(["SendAskRepeat", "WaitForRetrySignalReset"], errorCoordinator.Calls);
    }

    private static PreExecutionCoordinator CreateCoordinator(
        OpcUaSubscription subscription,
        out RecordingErrorCoordinator errorCoordinator)
    {
        var pauseToken = new PauseTokenSource();
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            TestInfrastructure.CreateHeartbeatHealthMonitor(),
            TestInfrastructure.CreateDualLogger<AutoReadySubscription>());
        var tagWaiter = new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());
        var pausableTagWaiter = new PausableTagWaiter(tagWaiter, pauseToken);
        errorCoordinator = new RecordingErrorCoordinator();
        var steps = new PreExecutionSteps(null!, null!, null!, null!, CreateStep(subscription));
        var infra = new PreExecutionInfrastructure(
            null!,
            null!,
            subscription,
            connectionState,
            autoReady,
            pausableTagWaiter,
            pauseToken,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            TestInfrastructure.CreateDualLogger<PreExecutionCoordinator>(),
            null!,
            null!);
        var coordinators = new PreExecutionCoordinators(
            null!,
            errorCoordinator,
            null!,
            CreateDialogCoordinator(),
            null!,
            null!,
            (TestCompletionUiState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(TestCompletionUiState)));
        var state = new PreExecutionState(
            (BoilerState)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BoilerState)),
            null!,
            new ExecutionActivityTracker(),
            new ExecutionPhaseState(),
            new ExecutionFlowState());
        return new PreExecutionCoordinator(
            steps,
            infra,
            coordinators,
            state,
            new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>()));
    }

    private static BlockBoilerAdapterStep CreateStep(OpcUaSubscription subscription)
    {
        var pauseToken = new PauseTokenSource();
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var tagWaiter = new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());
        var pausableTagWaiter = new PausableTagWaiter(tagWaiter, pauseToken);
        return new BlockBoilerAdapterStep(
            pausableTagWaiter,
            new ExecutionPhaseState(),
            TestInfrastructure.CreateDualLogger<BlockBoilerAdapterStep>());
    }

    private static ScanDialogCoordinator CreateDialogCoordinator()
    {
        var errorHandler = new ScanErrorHandler(new TestNotificationService());
        var scanBarcodeMesStep = (ScanBarcodeMesStep)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ScanBarcodeMesStep));
        return new ScanDialogCoordinator(errorHandler, scanBarcodeMesStep);
    }

    private sealed class RecordingErrorCoordinator : IErrorCoordinator
    {
        public List<string> Calls { get; } = [];
        public bool ThrowOnWaitForRetrySignalReset { get; set; }

        public event Action? OnReset
        {
            add { }
            remove { }
        }

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
        }

        public void ForceStop()
        {
        }

        public Task<ErrorResolution> WaitForResolutionAsync(WaitForResolutionOptions? options = null, CancellationToken ct = default)
        {
            return Task.FromResult(ErrorResolution.None);
        }

        public Task SendAskRepeatAsync(CancellationToken ct)
        {
            Calls.Add("SendAskRepeat");
            return Task.CompletedTask;
        }

        public Task SendAskRepeatAsync(string? blockErrorTag, CancellationToken ct)
        {
            Calls.Add("SendAskRepeat");
            return Task.CompletedTask;
        }

        public Task WaitForRetrySignalResetAsync(CancellationToken ct)
        {
            Calls.Add("WaitForRetrySignalReset");
            return ThrowOnWaitForRetrySignalReset
                ? Task.FromException(new TimeoutException("Req_Repeat timeout"))
                : Task.CompletedTask;
        }
    }
}
