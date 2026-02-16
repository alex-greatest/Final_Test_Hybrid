using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Wait;

/// <summary>
/// Ожидание в течение 3 секунд. Просто ждём три секунды.
/// </summary>
public class WaitThreeSecStep(DualLogger<WaitThreeSecStep> logger) : ITestStep
{
    public string Id => "wait-three-sec";
    public string Name => "Wait/WaitThreeSec";
    public string Description => "Ожидание в течение 3 секунд";

    /// <summary>
    /// Выполняет ожидание 3 секунды.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Начинаем ожидание 3 секунды...");
        await context.DelayAsync(TimeSpan.FromSeconds(3), ct);
        logger.LogInformation("Ожидание завершено");
        return TestStepResult.Pass("3 секунды");
    }
}
