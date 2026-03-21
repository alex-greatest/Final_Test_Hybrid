using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Final_Test_Hybrid.Models;
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
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Steps;
using Final_Test_Hybrid.Tests.TestSupport;
using Opc.Ua.Client;
using StepErrorResolution = Final_Test_Hybrid.Models.Steps.ErrorResolution;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PreExecutionAutoReadyGateTests
{
    private const string EndTag = "ns=3;s=\"DB_VI\".\"Block_Boiler_Adapter\".\"End\"";
    private const string ErrorTag = "ns=3;s=\"DB_VI\".\"Block_Boiler_Adapter\".\"Error\"";

    [Fact]
    public async Task EnsurePreExecutionInputReadyAsync_RaisesAutoModeDisabled_AndWaitsForResume()
    {
        var context = CreateCoordinatorContext();
        context.ConnectionState.SetConnected(true, "test");

        var waitTask = TestInfrastructure.InvokePrivateAsync(
            context.Coordinator,
            "EnsurePreExecutionInputReadyAsync",
            CancellationToken.None);

        await Task.Delay(100);

        Assert.False(waitTask.IsCompleted);
        Assert.True(context.PauseToken.IsPaused);
        Assert.Equal([InterruptReason.AutoModeDisabled], context.ErrorCoordinator.HandledReasons);
        Assert.Equal(InterruptReason.AutoModeDisabled, context.ErrorCoordinator.CurrentInterrupt);

        context.ErrorCoordinator.ResumeAutoModeDisabled();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.False(context.PauseToken.IsPaused);
        Assert.Null(context.ErrorCoordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task EnsurePreExecutionInputReadyAsync_DoesNotRaiseAutoModeDisabled_WhenPlcDisconnected()
    {
        var context = CreateCoordinatorContext();

        await TestInfrastructure.InvokePrivateAsync(
            context.Coordinator,
            "EnsurePreExecutionInputReadyAsync",
            CancellationToken.None);

        Assert.Empty(context.ErrorCoordinator.HandledReasons);
        Assert.False(context.PauseToken.IsPaused);
        Assert.Null(context.ErrorCoordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task EnsurePreExecutionInputReadyAsync_DoesNotOverrideExistingNonPlcInterrupt()
    {
        var context = CreateCoordinatorContext();
        context.ConnectionState.SetConnected(true, "test");
        context.ErrorCoordinator.SetCurrentInterrupt(InterruptReason.BoilerLock);
        context.PauseToken.Pause();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TestInfrastructure.InvokePrivateAsync(
                context.Coordinator,
                "EnsurePreExecutionInputReadyAsync",
                cts.Token));

        Assert.Empty(context.ErrorCoordinator.HandledReasons);
        Assert.Equal(InterruptReason.BoilerLock, context.ErrorCoordinator.CurrentInterrupt);
    }

    [Fact]
    public async Task BlockBoilerAdapterStep_WaitForCompletionAsync_IgnoresSignalsWhilePaused_AndContinuesAfterResume()
    {
        var context = CreateBlockStepContext();
        context.ConnectionState.SetConnected(true, "test");
        RegisterTrackedNode(context.Subscription, EndTag);
        RegisterTrackedNode(context.Subscription, ErrorTag);
        context.PauseToken.Pause();

        var waitTask = Assert.IsAssignableFrom<Task<PreExecutionResult>>(
            TestInfrastructure.InvokePrivate(
                context.Step,
                "WaitForCompletionAsync",
                new PreExecutionContext
                {
                    Barcode = "test",
                    BoilerState = (BoilerState)RuntimeHelpers.GetUninitializedObject(typeof(BoilerState)),
                    OpcUa = CreatePausableOpcUa(context.PauseToken),
                    TestStepLogger = new TestStepLoggerStub()
                },
                CancellationToken.None));

        await Task.Delay(100);
        PublishValue(context.Subscription, ErrorTag, true);
        await Task.Delay(100);

        Assert.False(waitTask.IsCompleted);

        context.PauseToken.Resume();
        var result = await waitTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(PreExecutionStatus.Failed, result.Status);
        Assert.True(result.IsRetryable);
        Assert.Equal("Ошибка блокировки адаптера", result.ErrorMessage);
    }

    private static CoordinatorContext CreateCoordinatorContext()
    {
        var pauseToken = new PauseTokenSource();
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        var autoReady = new AutoReadySubscription(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<AutoReadySubscription>());
        var errorCoordinator = new RecordingErrorCoordinator(pauseToken);
        var infra = new PreExecutionInfrastructure(
            null!,
            null!,
            subscription,
            connectionState,
            autoReady,
            null!,
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
        var coordinator = new PreExecutionCoordinator(
            new PreExecutionSteps(null!, null!, null!, null!, null!),
            infra,
            new PreExecutionCoordinators(
                null!,
                errorCoordinator,
                null!,
                null!,
                null!,
                null!,
                (TestCompletionUiState)RuntimeHelpers.GetUninitializedObject(typeof(TestCompletionUiState))),
            new PreExecutionState(
                (BoilerState)RuntimeHelpers.GetUninitializedObject(typeof(BoilerState)),
                null!,
                new ExecutionActivityTracker(),
                new ExecutionPhaseState(),
                new ExecutionFlowState()),
            new RuntimeTerminalState(TestInfrastructure.CreateDualLogger<RuntimeTerminalState>()));

        return new CoordinatorContext(coordinator, connectionState, pauseToken, errorCoordinator);
    }

    private static BlockStepContext CreateBlockStepContext()
    {
        var pauseToken = new PauseTokenSource();
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        var tagWaiter = new TagWaiter(
            subscription,
            connectionState,
            TestInfrastructure.CreateLogger<TagWaiter>(),
            new TestStepLoggerStub());
        var step = new BlockBoilerAdapterStep(
            new PausableTagWaiter(tagWaiter, pauseToken),
            new ExecutionPhaseState(),
            TestInfrastructure.CreateDualLogger<BlockBoilerAdapterStep>());

        return new BlockStepContext(step, subscription, connectionState, pauseToken);
    }

    private static PausableOpcUaTagService CreatePausableOpcUa(PauseTokenSource pauseToken)
    {
        return new PausableOpcUaTagService(
            (OpcUaTagService)RuntimeHelpers.GetUninitializedObject(typeof(OpcUaTagService)),
            pauseToken);
    }

    private static void RegisterTrackedNode(OpcUaSubscription subscription, string nodeId)
    {
        var monitoredItems = TestInfrastructure.GetPrivateField<ConcurrentDictionary<string, MonitoredItem>>(subscription, "_monitoredItems");
        monitoredItems[nodeId] = (MonitoredItem)RuntimeHelpers.GetUninitializedObject(typeof(MonitoredItem));
    }

    private static void PublishValue(OpcUaSubscription subscription, string nodeId, bool value)
    {
        var values = TestInfrastructure.GetSubscriptionValues(subscription);
        values[nodeId] = value;
        TestInfrastructure.InvokePrivate(subscription, "InvokeCallbacks", nodeId, value);
    }

    private sealed record CoordinatorContext(
        PreExecutionCoordinator Coordinator,
        OpcUaConnectionState ConnectionState,
        PauseTokenSource PauseToken,
        RecordingErrorCoordinator ErrorCoordinator);

    private sealed record BlockStepContext(
        BlockBoilerAdapterStep Step,
        OpcUaSubscription Subscription,
        OpcUaConnectionState ConnectionState,
        PauseTokenSource PauseToken);

    private sealed class RecordingErrorCoordinator(PauseTokenSource pauseToken) : IErrorCoordinator
    {
        public List<InterruptReason> HandledReasons { get; } = [];

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

        public InterruptReason? CurrentInterrupt { get; private set; }

        public Task HandleInterruptAsync(InterruptReason reason, CancellationToken ct = default)
        {
            HandledReasons.Add(reason);
            CurrentInterrupt = reason;
            if (reason == InterruptReason.AutoModeDisabled)
            {
                pauseToken.Pause();
            }
            return Task.CompletedTask;
        }

        public void SetCurrentInterrupt(InterruptReason reason)
        {
            CurrentInterrupt = reason;
        }

        public void ResumeAutoModeDisabled()
        {
            CurrentInterrupt = null;
            pauseToken.Resume();
        }

        public void Reset()
        {
        }

        public void ForceStop()
        {
        }

        public Task<StepErrorResolution> WaitForResolutionAsync(
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
}
