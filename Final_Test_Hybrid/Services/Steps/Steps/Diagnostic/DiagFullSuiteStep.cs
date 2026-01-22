using System.Diagnostics;
using System.Text;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Запускает полный набор диагностических тестов (9 шагов).
/// Останавливается при первом Fail и выводит итоговую таблицу результатов.
/// </summary>
public class DiagFullSuiteStep(
    DiagPingDataStep pingDataStep,
    DiagLatencyStep latencyStep,
    DiagStressReadStep stressReadStep,
    DiagConcurrentReadStep concurrentReadStep,
    DiagReadBoilerErrorsStep readBoilerErrorsStep,
    DiagWriteReadVerifyStep writeReadVerifyStep,
    DiagStartStopCycleStep startStopCycleStep,
    DiagReconnectRecoveryStep reconnectRecoveryStep,
    DiagPollingStressStep pollingStressStep,
    DualLogger<DiagFullSuiteStep> logger) : ITestStep
{
    public string Id => "diag-full-suite";
    public string Name => "DiagFullSuite";
    public string Description => "Полный набор диагностических тестов (9 шагов)";

    /// <summary>
    /// Выполняет все диагностические тесты последовательно.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        var totalSw = Stopwatch.StartNew();
        var results = new List<StepExecutionResult>();

        logger.LogInformation("═══════════════════════════════════════════════");
        logger.LogInformation("▶ СТАРТ ПОЛНОГО ДИАГНОСТИЧЕСКОГО НАБОРА (9 тестов)");
        logger.LogInformation("═══════════════════════════════════════════════");

        var steps = GetOrderedSteps();
        var allPassed = true;

        foreach (var (step, index) in steps.Select((s, i) => (s, i)))
        {
            ct.ThrowIfCancellationRequested();

            var result = await ExecuteStepAsync(step, index + 1, steps.Length, context, ct);
            results.Add(result);

            if (!result.Passed)
            {
                allPassed = false;
                logger.LogWarning("⚠ Тест [{Index}] {Name} FAIL — прерываем выполнение", index + 1, step.Name);
                break;
            }
        }

        totalSw.Stop();

        LogSummaryTable(results, totalSw.ElapsedMilliseconds);

        return EvaluateResults(results, allPassed, totalSw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Возвращает упорядоченный массив шагов.
    /// </summary>
    private ITestStep[] GetOrderedSteps() =>
    [
        pingDataStep,
        latencyStep,
        stressReadStep,
        concurrentReadStep,
        readBoilerErrorsStep,
        writeReadVerifyStep,
        startStopCycleStep,
        reconnectRecoveryStep,
        pollingStressStep
    ];

    /// <summary>
    /// Выполняет один диагностический шаг.
    /// </summary>
    private async Task<StepExecutionResult> ExecuteStepAsync(
        ITestStep step,
        int index,
        int total,
        TestStepContext context,
        CancellationToken ct)
    {
        logger.LogInformation("───────────────────────────────────────────────");
        logger.LogInformation("[{Index}/{Total}] ▶ {Name}: {Description}", index, total, step.Name, step.Description);
        logger.LogInformation("───────────────────────────────────────────────");

        var sw = Stopwatch.StartNew();

        try
        {
            var result = await step.ExecuteAsync(context, ct);
            sw.Stop();

            var status = result.Success ? "✓ PASS" : "✗ FAIL";
            logger.LogInformation("[{Index}/{Total}] {Status} — {Name} ({Elapsed}ms)",
                index, total, status, step.Name, sw.ElapsedMilliseconds);

            return new StepExecutionResult
            {
                Name = step.Name,
                Passed = result.Success,
                Message = result.Message,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "[{Index}/{Total}] ✗ EXCEPTION — {Name}: {Error}",
                index, total, step.Name, ex.Message);

            return new StepExecutionResult
            {
                Name = step.Name,
                Passed = false,
                Message = $"Exception: {ex.Message}",
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Логирует итоговую таблицу результатов.
    /// </summary>
    private void LogSummaryTable(List<StepExecutionResult> results, long totalMs)
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("                     ИТОГОВАЯ ТАБЛИЦА РЕЗУЛЬТАТОВ               ");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");
        sb.AppendLine("│ # │ Статус │ Название                    │ Время     │");
        sb.AppendLine("├───┼────────┼─────────────────────────────┼───────────┤");

        for (var i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var status = r.Passed ? "PASS" : "FAIL";
            var name = r.Name.PadRight(27);
            var time = $"{r.ElapsedMs}ms".PadLeft(9);
            sb.AppendLine($"│ {i + 1} │ {status}   │ {name} │ {time} │");
        }

        sb.AppendLine("├───┼────────┼─────────────────────────────┼───────────┤");

        var passedCount = results.Count(r => r.Passed);
        var failedCount = results.Count(r => !r.Passed);
        var totalSteps = GetOrderedSteps().Length;
        var notRun = totalSteps - results.Count;

        sb.AppendLine($"│ Выполнено: {results.Count}/{totalSteps}".PadRight(42) + $"│ {totalMs}ms".PadLeft(10) + " │");
        sb.AppendLine($"│ PASS: {passedCount}, FAIL: {failedCount}, Не запущено: {notRun}".PadRight(53) + "│");
        sb.AppendLine("═══════════════════════════════════════════════════════════════");

        logger.LogInformation(sb.ToString());
    }

    /// <summary>
    /// Оценивает общие результаты.
    /// </summary>
    private TestStepResult EvaluateResults(List<StepExecutionResult> results, bool allPassed, long totalMs)
    {
        var passedCount = results.Count(r => r.Passed);
        var failedCount = results.Count(r => !r.Passed);
        var totalSteps = GetOrderedSteps().Length;

        var summary = $"Passed={passedCount}/{totalSteps}, Failed={failedCount}, TotalTime={totalMs}ms";

        if (allPassed && results.Count == totalSteps)
        {
            logger.LogInformation("◼ ВСЕ 9 ТЕСТОВ ПРОЙДЕНЫ УСПЕШНО!");
            return TestStepResult.Pass(summary);
        }

        var failedStep = results.FirstOrDefault(r => !r.Passed);
        var failMessage = failedStep != null
            ? $"Fail at [{failedStep.Name}]: {failedStep.Message}"
            : "Incomplete";

        logger.LogError("◼ ДИАГНОСТИКА ЗАВЕРШЕНА С ОШИБКАМИ: {Message}", failMessage);
        return TestStepResult.Fail($"{failMessage}. {summary}");
    }

    /// <summary>
    /// Результат выполнения одного шага.
    /// </summary>
    private class StepExecutionResult
    {
        public required string Name { get; init; }
        public bool Passed { get; init; }
        public string? Message { get; init; }
        public long ElapsedMs { get; init; }
    }
}
