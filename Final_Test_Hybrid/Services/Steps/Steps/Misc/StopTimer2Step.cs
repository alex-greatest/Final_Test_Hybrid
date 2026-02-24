using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Results;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

public class StopTimer2Step(
    ITimerService timerService,
    ITestResultsService testResultsService,
    DualLogger<StopTimer2Step> logger) : ITestStep
{
    private const string TimerKey = "Timer2";

    public string Id => "stop-timer-2";
    public string Name => "Misc/StopTimer2";
    public string Description => "Остановка таймера 2";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var elapsed = timerService.Stop(TimerKey);
        if (elapsed == null)
        {
            logger.LogWarning("Таймер 2 не был запущен");
            return Task.FromResult(TestStepResult.Pass("Таймер не был запущен"));
        }

        var seconds = elapsed.Value.TotalSeconds;
        logger.LogInformation("Таймер 2 остановлен: {Seconds:F2} сек", seconds);

        testResultsService.Remove("Timer_2");
        testResultsService.Add("Timer_2", $"{seconds:F2}", "", "", 1, false, "сек");

        return Task.FromResult(TestStepResult.Pass($"{seconds:F2} сек"));
    }
}
