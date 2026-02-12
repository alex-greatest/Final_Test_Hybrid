using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Wait;

/// <summary>
/// Ожидание в течение 10 секунд.
/// </summary>
public class WaitTenSecStep(DualLogger<WaitTenSecStep> logger) : ITestStep
{
    public string Id => "wait-ten-sec";
    public string Name => "Wait/WaitTenSec";
    public string Description => "Ожидание в течение 10 секунд.";

    /// <summary>
    /// Выполняет ожидание 10 секунд.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Начинаем ожидание 10 секунд...");
        await context.DelayAsync(TimeSpan.FromSeconds(10), ct);
        logger.LogInformation("Ожидание завершено");
        return TestStepResult.Pass("10 секунд");
    }
}
