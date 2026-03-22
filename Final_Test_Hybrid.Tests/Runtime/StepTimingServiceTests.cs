using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Tests.Runtime;

public sealed class StepTimingServiceTests
{
    [Fact]
    public void AddCompletedStepTiming_AddsFinishedRecordWithFormattedZeroDuration()
    {
        using var service = new StepTimingService();

        service.AddCompletedStepTiming("Misc/StartTimer1", "Запуск таймера 1", TimeSpan.Zero);

        var records = service.GetAll();

        var record = Assert.Single(records);
        Assert.Equal("Misc/StartTimer1", record.Name);
        Assert.Equal("Запуск таймера 1", record.Description);
        Assert.Equal("00.00", record.Duration);
        Assert.False(record.IsRunning);
    }

    [Fact]
    public void AddCompletedStepTiming_DoesNotMakeStepActive()
    {
        using var service = new StepTimingService();

        service.AddCompletedStepTiming("Misc/StartTimer1", "Запуск таймера 1", TimeSpan.Zero);

        Assert.False(service.HasActiveStep("Misc/StartTimer1"));
    }
}
