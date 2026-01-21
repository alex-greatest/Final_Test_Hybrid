using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Выполняет 100 последовательных чтений регистра с логированием каждой итерации.
/// </summary>
public class DiagStressReadStep(DualLogger<DiagStressReadStep> logger) : ITestStep
{
    private const int Iterations = 100;
    private const int DelayMs = 100;
    private const int MaxConsecutiveFails = 5;
    private const ushort ModeKeyAddress = 0x1000;

    public string Id => "diag-stress-read";
    public string Name => "DiagStressRead";
    public string Description => "Нагрузочное тестирование чтения Modbus";

    /// <summary>
    /// Выполняет нагрузочный тест чтения регистров.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var successCount = 0;
        var failCount = 0;
        var consecutiveFails = 0;
        var sw = Stopwatch.StartNew();

        logger.LogInformation("▶ Старт нагрузочного теста: {Iterations} чтений", Iterations);

        for (var i = 0; i < Iterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            var result = await context.DiagReader.ReadUInt16Async(ModeKeyAddress, ct: ct);

            if (result.Success)
            {
                successCount++;
                consecutiveFails = 0;
                logger.LogDebug("[{Index}/{Total}] OK: 0x{Value:X4}", i + 1, Iterations, result.Value);
            }
            else
            {
                failCount++;
                consecutiveFails++;
                logger.LogWarning("[{Index}/{Total}] FAIL: {Error}", i + 1, Iterations, result.Error);

                if (consecutiveFails >= MaxConsecutiveFails)
                {
                    logger.LogError("◼ Прервано: {MaxFails} последовательных ошибок подряд", MaxConsecutiveFails);
                    sw.Stop();
                    return TestStepResult.Fail($"Прервано после {consecutiveFails} последовательных ошибок. Success: {successCount}, Fail: {failCount}");
                }
            }

            await context.DelayAsync(TimeSpan.FromMilliseconds(DelayMs), ct);
        }

        sw.Stop();

        logger.LogInformation(
            "◼ Завершено за {Elapsed}мс. Success: {Success}, Fail: {Fail}",
            sw.ElapsedMilliseconds, successCount, failCount);

        return failCount == 0
            ? TestStepResult.Pass($"Success: {successCount}/{Iterations}")
            : TestStepResult.Fail($"Ошибок: {failCount}/{Iterations}");
    }
}
