using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Misc;

/// <summary>
/// Ожидание в течение 1 секунды.
/// </summary>
public class WaitOneSecStep(DualLogger<WaitOneSecStep> logger) : ITestStep
{
    public string Id => "wait-one-sec";
    public string Name => "WaitOneSec";
    public string Description => "Ожидание в течение 1 секунды.";

    /// <summary>
    /// Выполняет ожидание 1 секунду.
    /// </summary>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Начинаем ожидание 1 секунду...");
        await context.DelayAsync(TimeSpan.FromSeconds(1), ct);
        logger.LogInformation("Ожидание завершено");
        return TestStepResult.Pass("1 секунда");
    }
}
