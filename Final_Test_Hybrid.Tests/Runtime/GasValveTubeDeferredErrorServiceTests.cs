using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Errors;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.OpcUa.Subscription;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class GasValveTubeDeferredErrorServiceTests
{
    [Fact]
    public async Task TrueThenFalseBeforeDelay_KeepsErrorInactiveAndClearsMessage()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, _, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.RelatedStepName!,
            "test");

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax, true);

        Assert.True(service.IsMessageActive);
        Assert.Empty(errorService.RaisedPlcCodes);

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax, false);

        Assert.False(service.IsMessageActive);
        Assert.Empty(errorService.RaisedPlcCodes);
    }

    [Fact]
    public async Task CompletedDelay_RaisesPlcErrorAndKeepsMessageUntilFalse()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, _, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.RelatedStepName!,
            "test");

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin, true);
        delayTcs.SetResult();
        await Task.Yield();

        Assert.True(service.IsMessageActive);
        Assert.Contains(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code,
            errorService.RaisedPlcCodes);

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin, false);

        Assert.False(service.IsMessageActive);
        Assert.Contains(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code,
            errorService.ClearedPlcCodes);
    }

    [Fact]
    public async Task RaisedError_StepExitClearsActivePlcErrorEvenIfTagStillTrue()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, _, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.RelatedStepName!,
            "test");

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax, true);
        delayTcs.SetResult();
        await WaitForAsync(
            () => errorService.RaisedPlcCodes.Contains(
                ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.Code));

        stepTimingService.StopCurrentStepTiming();
        await Task.Yield();

        Assert.False(service.IsMessageActive);
        Assert.Contains(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.Code,
            errorService.ClearedPlcCodes);
    }

    [Fact]
    public async Task RaisedError_ConnectionLostClearsActivePlcErrorEvenIfTagStillTrue()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, connectionState, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.RelatedStepName!,
            "test");

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin, true);
        delayTcs.SetResult();
        await WaitForAsync(
            () => errorService.RaisedPlcCodes.Contains(
                ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code));

        connectionState.SetConnected(false, "test");
        await Task.Yield();

        Assert.False(service.IsMessageActive);
        Assert.Contains(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code,
            errorService.ClearedPlcCodes);
    }

    [Fact]
    public async Task ConnectionLost_CancelsPendingDelayAndHidesMessage()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, connectionState, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));

        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.RelatedStepName!,
            "test");
        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax, true);

        Assert.True(service.IsMessageActive);

        connectionState.SetConnected(false, "test");
        await Task.Yield();

        Assert.False(service.IsMessageActive);

        delayTcs.SetResult();
        await Task.Delay(10);

        Assert.Empty(errorService.RaisedPlcCodes);
    }

    [Fact]
    public async Task StepExit_CancelsPendingDelayAndHidesMessage()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, _, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));
        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax.RelatedStepName!,
            "test");

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMax, true);

        Assert.True(service.IsMessageActive);

        stepTimingService.StopCurrentStepTiming();
        await Task.Yield();

        Assert.False(service.IsMessageActive);

        delayTcs.SetResult();
        await Task.Delay(10);

        Assert.Empty(errorService.RaisedPlcCodes);
    }

    [Fact]
    public async Task StepBecomesActiveAfterTag_StartsDeferredFlow()
    {
        var delayTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (service, _, errorService, stepTimingService) = CreateService(
            (_, ct) => delayTcs.Task.WaitAsync(ct));

        await service.ProcessTagChangedAsync(ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin, true);

        Assert.False(service.IsMessageActive);
        Assert.Empty(errorService.RaisedPlcCodes);

        stepTimingService.StartCurrentStepTiming(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.RelatedStepName!,
            "test");
        await Task.Yield();

        Assert.True(service.IsMessageActive);

        delayTcs.SetResult();
        await WaitForAsync(
            () => errorService.RaisedPlcCodes.Contains(
                ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code));

        Assert.Contains(
            ErrorDefinitions.AlNotConnectSensorPgbSetGasBurnerMin.Code,
            errorService.RaisedPlcCodes);
    }

    private static (
        GasValveTubeDeferredErrorService Service,
        OpcUaConnectionState ConnectionState,
        TestErrorService ErrorService,
        IStepTimingService StepTimingService) CreateService(
        Func<TimeSpan, CancellationToken, Task> delayAsync)
    {
        var connectionState = new OpcUaConnectionState(
            TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var subscription = new OpcUaSubscription(
            connectionState,
            TestInfrastructure.CreateOpcUaOptions(),
            TestInfrastructure.CreateDualLogger<OpcUaSubscription>());
        var errorService = new TestErrorService();
        var stepTimingService = new StepTimingService();

        var service = new GasValveTubeDeferredErrorService(
            subscription,
            connectionState,
            stepTimingService,
            errorService,
            TestInfrastructure.CreateLogger<GasValveTubeDeferredErrorService>(),
            TimeSpan.FromSeconds(30),
            delayAsync);

        return (service, connectionState, errorService, stepTimingService);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 20; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(10);
        }
    }
}
