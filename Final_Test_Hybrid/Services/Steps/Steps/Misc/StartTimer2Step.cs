using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

public class StartTimer2Step(
    ITimerService timerService,
    DualLogger<StartTimer2Step> logger) : IPreExecutionStep
{
    private const string TimerKey = "Timer2";

    public string Id => "start-timer-2";
    public string Name => "StartTimer2";
    public string Description => "Запуск таймера 2";
    public bool IsVisibleInStatusGrid => true;
    public bool IsSkippable => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        timerService.Start(TimerKey);
        logger.LogInformation("Таймер 2 запущен");
        return Task.FromResult(PreExecutionResult.Continue());
    }
}
