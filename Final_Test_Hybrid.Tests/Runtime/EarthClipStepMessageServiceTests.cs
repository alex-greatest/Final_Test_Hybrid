using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class EarthClipStepMessageServiceTests
{
    [Fact]
    public void Activate_InsideActiveStep_ShowsMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(EarthClipStepMessageService.EarthClipStepName, "test");

        service.Activate();

        Assert.True(service.IsMessageActive);
    }

    [Fact]
    public void Deactivate_HidesMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(EarthClipStepMessageService.EarthClipStepName, "test");
        service.Activate();

        service.Deactivate();

        Assert.False(service.IsMessageActive);
    }

    [Fact]
    public void StopCurrentStepTiming_HidesMessageWithoutExplicitDeactivate()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(EarthClipStepMessageService.EarthClipStepName, "test");
        service.Activate();

        stepTimingService.StopCurrentStepTiming();

        Assert.False(service.IsMessageActive);
    }

    [Fact]
    public void ConnectionLost_HidesMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(EarthClipStepMessageService.EarthClipStepName, "test");
        service.Activate();

        connectionState.SetConnected(false, "test");

        Assert.False(service.IsMessageActive);
    }

    [Fact]
    public void Activate_OutsideActiveStep_DoesNotShowMessage()
    {
        var (service, connectionState, _) = CreateService();
        connectionState.SetConnected(true, "test");

        service.Activate();

        Assert.False(service.IsMessageActive);
    }

    private static (
        EarthClipStepMessageService Service,
        OpcUaConnectionState ConnectionState,
        StepTimingService StepTimingService) CreateService()
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var stepTimingService = new StepTimingService();
        var service = new EarthClipStepMessageService(
            connectionState,
            stepTimingService,
            TestInfrastructure.CreateLogger<EarthClipStepMessageService>());

        return (service, connectionState, stepTimingService);
    }
}
