using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Wait;

/// <summary>
/// Ожидание в течение 5 секунд.
/// </summary>
public class WaitFiveSecStep(DualLogger<WaitFiveSecStep> logger) : ITestStep
{
    public string Id => "wait-five-sec";
    public string Name => "WaitTime/WaitFiveSec";
    public string Description => "Ожидание в течение 5 секунд.";

    /// <summary>
    /// Выполняет ожидание 5 секунд.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Начинаем ожидание 5 секунд...");
        await context.DelayAsync(TimeSpan.FromSeconds(5), ct);
        logger.LogInformation("Ожидание завершено");
        return TestStepResult.Pass("5 секунд");
    }
}
