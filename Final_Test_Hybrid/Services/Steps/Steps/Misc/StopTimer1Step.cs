using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

public class StopTimer1Step(
    ITimerService timerService,
    ITestResultsService testResultsService,
    DualLogger<StopTimer1Step> logger) : ITestStep
{
    private const string TimerKey = "Timer1";

    public string Id => "stop-timer-1";
    public string Name => "Misc/StopTimer1";
    public string Description => "Остановка таймера 1";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var elapsed = timerService.Stop(TimerKey);
        if (elapsed == null)
        {
            logger.LogWarning("Таймер 1 не был запущен");
            return Task.FromResult(TestStepResult.Pass("Таймер не был запущен"));
        }

        var seconds = elapsed.Value.TotalSeconds;
        logger.LogInformation("Таймер 1 остановлен: {Seconds:F2} сек", seconds);

        testResultsService.Remove("Timer_1");
        testResultsService.Add(
            parameterName: "Timer_1",
            value: $"{seconds:F2}",
            min: "",
            max: "",
            status: 1,
            isRanged: false,
            unit: "сек",
            test: Name);

        return Task.FromResult(TestStepResult.Pass($"{seconds:F2} сек"));
    }
}
