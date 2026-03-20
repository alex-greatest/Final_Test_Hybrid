using Final_Test_Hybrid.Services.Common;
using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.PreExecution;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PreExecutionStopReasonTests
{
    [Fact]
    public void ResolveStopExitReasonOrFallback_UsesResetSignalSnapshot_WhenFieldChangesAfterCapture()
    {
        var coordinator = CreateCoordinator(TestInfrastructure.CreateSubscription());
        var snapshot = CreateCompletedResetSignal(CycleExitReason.HardReset);
        TestInfrastructure.SetPrivateField(coordinator, "_resetSignal", snapshot);
        TestInfrastructure.SetPrivateField(coordinator, "_resetSignal", new TaskCompletionSource<CycleExitReason>(TaskCreationOptions.RunContinuationsAsynchronously));

        var reason = Assert.IsType<CycleExitReason>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "ResolveStopExitReasonOrFallback",
                CycleExitReason.PipelineCancelled,
                snapshot));

        Assert.Equal(CycleExitReason.HardReset, reason);
    }

    [Fact]
    public void ResolveStopExitReasonOrFallback_PrefersPendingExitReason_OverResetSignalSnapshot()
    {
        var coordinator = CreateCoordinator(TestInfrastructure.CreateSubscription());
        var snapshot = CreateCompletedResetSignal(CycleExitReason.SoftReset);
        TestInfrastructure.SetPrivateField(coordinator, "_pendingExitReason", (int)CycleExitReason.RepeatRequested);

        var reason = Assert.IsType<CycleExitReason>(
            TestInfrastructure.InvokePrivate(
                coordinator,
                "ResolveStopExitReasonOrFallback",
                CycleExitReason.PipelineCancelled,
                snapshot));

        Assert.Equal(CycleExitReason.RepeatRequested, reason);
    }

    private static TaskCompletionSource<CycleExitReason> CreateCompletedResetSignal(CycleExitReason reason)
    {
        var signal = new TaskCompletionSource<CycleExitReason>(TaskCreationOptions.RunContinuationsAsynchronously);
        signal.TrySetResult(reason);
        return signal;
    }

    private static PreExecutionCoordinator CreateCoordinator(OpcUaSubscription subscription)
    {
        var steps = new PreExecutionSteps(null!, null!, null!, null!, null!);
        var infra = new PreExecutionInfrastructure(
            null!,
            null!,
            subscription,
            null!,
            new PauseTokenSource(),
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
        var coordinators = new PreExecutionCoordinators(null!, null!, null!, null!, null!, null!, null!);
        var state = new PreExecutionState(null!, null!, new ExecutionActivityTracker(), new ExecutionPhaseState(), new ExecutionFlowState());
        return new PreExecutionCoordinator(
            steps,
            infra,
            coordinators,
            state,
            new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>()));
    }
}
