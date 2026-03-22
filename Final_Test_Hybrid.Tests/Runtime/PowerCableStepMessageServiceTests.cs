using Final_Test_Hybrid.Services.Main.Messages;
using Final_Test_Hybrid.Services.OpcUa.Connection;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;
using Final_Test_Hybrid.Tests.TestSupport;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class PowerCableStepMessageServiceTests
{
    [Fact]
    public void Activate_InsideActiveStep_ShowsMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(PowerCableStepMessageService.PowerCableStepName, "test");

        service.Activate();

        Assert.True(service.IsMessageActive);
    }

    [Fact]
    public void Deactivate_HidesMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(PowerCableStepMessageService.PowerCableStepName, "test");
        service.Activate();

        service.Deactivate();

        Assert.False(service.IsMessageActive);
    }

    [Fact]
    public void StopCurrentStepTiming_HidesMessageWithoutExplicitDeactivate()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(PowerCableStepMessageService.PowerCableStepName, "test");
        service.Activate();

        stepTimingService.StopCurrentStepTiming();

        Assert.False(service.IsMessageActive);
    }

    [Fact]
    public void ConnectionLost_HidesMessage()
    {
        var (service, connectionState, stepTimingService) = CreateService();
        connectionState.SetConnected(true, "test");
        stepTimingService.StartCurrentStepTiming(PowerCableStepMessageService.PowerCableStepName, "test");
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
        PowerCableStepMessageService Service,
        OpcUaConnectionState ConnectionState,
        StepTimingService StepTimingService) CreateService()
    {
        var connectionState = new OpcUaConnectionState(TestInfrastructure.CreateLogger<OpcUaConnectionState>());
        var stepTimingService = new StepTimingService();
        var service = new PowerCableStepMessageService(
            connectionState,
            stepTimingService,
            TestInfrastructure.CreateLogger<PowerCableStepMessageService>());

        return (service, connectionState, stepTimingService);
    }
}
