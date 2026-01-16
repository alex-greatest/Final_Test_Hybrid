using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.PreExecution;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Timing;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

public class StartTimer1Step(
    ITimerService timerService,
    DualLogger<StartTimer1Step> logger) : IPreExecutionStep
{
    private const string TimerKey = "Timer1";

    public string Id => "start-timer-1";
    public string Name => "StartTimer1";
    public string Description => "Запуск таймера 1";
    public bool IsVisibleInStatusGrid => true;
    public bool IsSkippable => false;

    public Task<PreExecutionResult> ExecuteAsync(PreExecutionContext context, CancellationToken ct)
    {
        timerService.Start(TimerKey);
        logger.LogInformation("Таймер 1 запущен");
        return Task.FromResult(PreExecutionResult.Continue());
    }
}
