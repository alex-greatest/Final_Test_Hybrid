using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Выполняет 5 циклов Start - Delay - Stop для проверки корректного рестарта диспетчера.
/// </summary>
public class DiagStartStopCycleStep(
    IModbusDispatcher dispatcher,
    DualLogger<DiagStartStopCycleStep> logger) : ITestStep
{
    private const int Cycles = 100;
    private const int DelayMs = 1000;

    public string Id => "diag-start-stop-cycle";
    public string Name => "DiagStartStopCycle";
    public string Description => "Тест рестарта ModbusDispatcher";

    /// <summary>
    /// Выполняет тест циклов запуска и остановки диспетчера.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Старт теста рестарта: {Cycles} циклов", Cycles);

        for (var i = 0; i < Cycles; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (dispatcher.IsStarted)
            {
                logger.LogDebug("[{Cycle}] Stopping...", i + 1);
                var swStop = Stopwatch.StartNew();
                await dispatcher.StopAsync();
                logger.LogDebug("[{Cycle}] Stopped in {Elapsed}ms", i + 1, swStop.ElapsedMilliseconds);
            }

            logger.LogDebug("[{Cycle}] Starting...", i + 1);
            var swStart = Stopwatch.StartNew();
            await dispatcher.StartAsync(ct);
            logger.LogDebug("[{Cycle}] Started in {Elapsed}ms", i + 1, swStart.ElapsedMilliseconds);

            await context.DelayAsync(TimeSpan.FromMilliseconds(DelayMs), ct);

            logger.LogInformation(
                "[{Cycle}/{Total}] IsStarted={IsStarted}, IsConnected={IsConnected}",
                i + 1, Cycles, dispatcher.IsStarted, dispatcher.IsConnected);
        }

        logger.LogInformation("◼ Тест рестарта завершён успешно");
        return TestStepResult.Pass($"Выполнено {Cycles} циклов рестарта");
    }
}
