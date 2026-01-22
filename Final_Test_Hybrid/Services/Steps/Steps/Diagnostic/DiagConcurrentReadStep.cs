using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Проверяет thread-safety через параллельные чтения Modbus.
/// Выполняет 20 параллельных операций чтения через Task.WhenAll.
/// </summary>
public class DiagConcurrentReadStep(DualLogger<DiagConcurrentReadStep> logger) : ITestStep
{
    private const int ConcurrentTasks = 20;
    private const ushort TestAddress = 0x1000;

    public string Id => "diag-concurrent-read";
    public string Name => "DiagConcurrentRead";
    public string Description => "Параллельное чтение Modbus (thread-safety)";

    /// <summary>
    /// Выполняет параллельные операции чтения.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Старт параллельного чтения: {Tasks} задач", ConcurrentTasks);

        var sw = Stopwatch.StartNew();
        var results = new ConcurrentReadResult[ConcurrentTasks];
        var exceptions = new List<Exception>();

        var tasks = Enumerable.Range(0, ConcurrentTasks)
            .Select(i => ExecuteSingleReadAsync(context, i, results, ct))
            .ToArray();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            exceptions.Add(ex);
            logger.LogError(ex, "Исключение при параллельном чтении: {Error}", ex.Message);
        }

        sw.Stop();

        CollectExceptions(tasks, exceptions);

        var successCount = results.Count(r => r.Success);
        var failCount = ConcurrentTasks - successCount;
        var throughput = successCount / (sw.Elapsed.TotalSeconds + 0.001);

        LogResults(successCount, failCount, sw.ElapsedMilliseconds, throughput, exceptions.Count);

        return EvaluateResults(successCount, failCount, throughput, exceptions);
    }

    /// <summary>
    /// Выполняет одну операцию чтения.
    /// </summary>
    private async Task ExecuteSingleReadAsync(
        TestStepContext context,
        int index,
        ConcurrentReadResult[] results,
        CancellationToken ct)
    {
        var taskSw = Stopwatch.StartNew();

        try
        {
            var result = await context.DiagReader.ReadUInt16Async(TestAddress, ct: ct);
            taskSw.Stop();

            results[index] = new ConcurrentReadResult
            {
                Success = result.Success,
                LatencyMs = taskSw.ElapsedMilliseconds,
                Error = result.Error
            };

            logger.LogDebug("[Task {Index}] {Status} in {Latency}ms",
                index, result.Success ? "OK" : "FAIL", taskSw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            taskSw.Stop();
            results[index] = new ConcurrentReadResult
            {
                Success = false,
                LatencyMs = taskSw.ElapsedMilliseconds,
                Error = ex.Message
            };

            logger.LogWarning("[Task {Index}] Exception: {Error}", index, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Собирает исключения из завершённых задач.
    /// </summary>
    private static void CollectExceptions(Task[] tasks, List<Exception> exceptions)
    {
        foreach (var task in tasks.Where(t => t.IsFaulted))
        {
            if (task.Exception?.InnerExceptions != null)
            {
                exceptions.AddRange(task.Exception.InnerExceptions);
            }
        }
    }

    /// <summary>
    /// Логирует результаты теста.
    /// </summary>
    private void LogResults(int success, int fail, long totalMs, double throughput, int exceptionCount)
    {
        logger.LogInformation(
            "◼ Результаты параллельного чтения:\n" +
            "  Success: {Success}/{Total}\n" +
            "  Fail: {Fail}\n" +
            "  Total time: {TotalMs}ms\n" +
            "  Throughput: {Throughput:F2} ops/sec\n" +
            "  Exceptions: {Exceptions}",
            success, ConcurrentTasks, fail, totalMs, throughput, exceptionCount);
    }

    /// <summary>
    /// Оценивает результаты теста.
    /// </summary>
    private TestStepResult EvaluateResults(int success, int fail, double throughput, List<Exception> exceptions)
    {
        var summary = $"Success={success}/{ConcurrentTasks}, Throughput={throughput:F2} ops/sec";

        if (exceptions.Count > 0)
        {
            return TestStepResult.Fail($"Исключения при параллельном чтении: {exceptions.Count}. {summary}");
        }

        return fail > 0 ? TestStepResult.Fail($"Ошибки при параллельном чтении: {fail}. {summary}") : TestStepResult.Pass(summary);
    }

    /// <summary>
    /// Результат одной операции чтения.
    /// </summary>
    private struct ConcurrentReadResult
    {
        public bool Success;
        public long LatencyMs;
        public string? Error;
    }
}
