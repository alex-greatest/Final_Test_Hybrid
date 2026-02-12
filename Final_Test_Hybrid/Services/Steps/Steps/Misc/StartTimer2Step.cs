using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

public class StartTimer2Step(
    ITimerService timerService,
    DualLogger<StartTimer2Step> logger) : ITestStep
{
    private const string TimerKey = "Timer2";

    public string Id => "start-timer-2";
    public string Name => "Misc/StartTimer2";
    public string Description => "Запуск таймера 2";

    public Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        timerService.Start(TimerKey);
        logger.LogInformation("Таймер 2 запущен");
        return Task.FromResult(TestStepResult.Pass("Таймер 2 запущен"));
    }
}
