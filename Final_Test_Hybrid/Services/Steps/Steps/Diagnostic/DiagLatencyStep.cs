using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Измеряет latency операций чтения Modbus.
/// Выполняет 100 итераций и рассчитывает Min/Max/Avg/P95/P99.
/// </summary>
public class DiagLatencyStep(DualLogger<DiagLatencyStep> logger) : ITestStep
{
    private const int Iterations = 100;
    private const ushort TestAddress = 0x1000;
    private const int MaxAvgMs = 500;
    private const int MaxP99Ms = 2000;

    public string Id => "diag-latency";
    public string Name => "DiagLatency";
    public string Description => "Измерение latency операций чтения Modbus";

    /// <summary>
    /// Выполняет измерение latency операций чтения.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Старт измерения latency: {Iterations} итераций", Iterations);

        var latencies = new List<long>(Iterations);
        var failCount = 0;

        for (var i = 0; i < Iterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            var sw = Stopwatch.StartNew();
            var result = await context.DiagReader.ReadUInt16Async(TestAddress, ct: ct);
            sw.Stop();

            if (result.Success)
            {
                latencies.Add(sw.ElapsedMilliseconds);
                logger.LogDebug("[{Index}/{Total}] {Latency}ms", i + 1, Iterations, sw.ElapsedMilliseconds);
            }
            else
            {
                failCount++;
                logger.LogWarning("[{Index}/{Total}] FAIL: {Error}", i + 1, Iterations, result.Error);
            }
        }

        if (latencies.Count == 0)
        {
            logger.LogError("◼ Все {Iterations} чтений завершились ошибкой", Iterations);
            return TestStepResult.Fail("Все операции чтения завершились ошибкой");
        }

        var metrics = CalculateMetrics(latencies);
        LogMetrics(metrics, failCount);

        return EvaluateResults(metrics, failCount);
    }

    /// <summary>
    /// Рассчитывает метрики latency.
    /// </summary>
    private static LatencyMetrics CalculateMetrics(List<long> latencies)
    {
        var sorted = latencies.OrderBy(x => x).ToList();
        var count = sorted.Count;

        return new LatencyMetrics
        {
            Min = sorted[0],
            Max = sorted[count - 1],
            Avg = (long)sorted.Average(),
            P95 = sorted[(int)(count * 0.95)],
            P99 = sorted[(int)(count * 0.99)]
        };
    }

    /// <summary>
    /// Логирует метрики latency.
    /// </summary>
    private void LogMetrics(LatencyMetrics metrics, int failCount)
    {
        logger.LogInformation(
            "◼ Результаты latency:\n" +
            "  Min: {Min}ms\n" +
            "  Max: {Max}ms\n" +
            "  Avg: {Avg}ms\n" +
            "  P95: {P95}ms\n" +
            "  P99: {P99}ms\n" +
            "  Ошибок: {Fail}/{Total}",
            metrics.Min, metrics.Max, metrics.Avg, metrics.P95, metrics.P99, failCount, Iterations);
    }

    /// <summary>
    /// Оценивает результаты теста.
    /// </summary>
    private TestStepResult EvaluateResults(LatencyMetrics metrics, int failCount)
    {
        var summary = $"Min={metrics.Min}ms, Max={metrics.Max}ms, Avg={metrics.Avg}ms, P95={metrics.P95}ms, P99={metrics.P99}ms, Errors={failCount}";

        if (metrics.Avg > MaxAvgMs)
        {
            return TestStepResult.Fail($"Avg latency {metrics.Avg}ms > {MaxAvgMs}ms. {summary}");
        }

        if (metrics.P99 > MaxP99Ms)
        {
            return TestStepResult.Fail($"P99 latency {metrics.P99}ms > {MaxP99Ms}ms. {summary}");
        }

        return TestStepResult.Pass(summary);
    }

    /// <summary>
    /// Метрики latency.
    /// </summary>
    private readonly record struct LatencyMetrics
    {
        public long Min { get; init; }
        public long Max { get; init; }
        public long Avg { get; init; }
        public long P95 { get; init; }
        public long P99 { get; init; }
    }
}
