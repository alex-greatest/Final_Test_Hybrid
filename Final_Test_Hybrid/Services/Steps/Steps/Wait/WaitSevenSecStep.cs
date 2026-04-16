using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Wait;

/// <summary>
/// Ожидание в течение 7 секунд.
/// </summary>
public class WaitSevenSecStep(DualLogger<WaitSevenSecStep> logger) : ITestStep
{
    public string Id => "wait-seven-sec";
    public string Name => "WaitTime/WaitSevenSec";
    public string Description => "Ожидание в течение 7 секунд.";

    /// <summary>
    /// Выполняет ожидание 7 секунд.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Начинаем ожидание 7 секунд...");
        await context.DelayAsync(TimeSpan.FromSeconds(7), ct);
        logger.LogInformation("Ожидание завершено");
        return TestStepResult.Pass("7 секунд");
    }
}
