using System.Diagnostics;
using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.Diagnostic.Protocol.CommandQueue;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Interfaces.Test;
using Final_Test_Hybrid.Services.Steps.Infrastructure.Registrator;

namespace Final_Test_Hybrid.Services.Steps.Steps.Diagnostic;

/// <summary>
/// Проверяет восстановление связи после разрыва.
/// Останавливает диспетчер, запускает и проверяет восстановление за 10 секунд.
/// </summary>
public class DiagReconnectRecoveryStep(
    IModbusDispatcher dispatcher,
    DualLogger<DiagReconnectRecoveryStep> logger) : ITestStep
{
    private const int RecoveryTimeoutMs = 10_000;
    private const int PollIntervalMs = 500;
    private const ushort TestAddress = 0x1000;

    public string Id => "diag-reconnect-recovery";
    public string Name => "DiagReconnectRecovery";
    public string Description => "Тест восстановления связи ModbusDispatcher";

    /// <summary>
    /// Выполняет тест восстановления связи.
    /// </summary>
    /// <param name="context">Контекст выполнения шага.</param>
    /// <param name="ct">Токен отмены.</param>
    /// <returns>Результат выполнения шага.</returns>
    public async Task<TestStepResult> ExecuteAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("▶ Старт теста восстановления связи");

        var wasConnected = dispatcher.IsConnected;
        logger.LogInformation("Исходное состояние: IsStarted={IsStarted}, IsConnected={IsConnected}",
            dispatcher.IsStarted, dispatcher.IsConnected);

        var stopResult = await SimulateDisconnectAsync();

        if (!stopResult.Success)
        {
            return TestStepResult.Fail($"Ошибка при остановке: {stopResult.Error}");
        }

        logger.LogInformation("Диспетчер остановлен за {Elapsed}ms", stopResult.ElapsedMs);

        var startResult = await StartAndWaitForRecoveryAsync(context, ct);

        if (!startResult.Success)
        {
            return TestStepResult.Fail(startResult.Error!);
        }

        var readResult = await VerifyConnectionWithReadAsync(context, ct);

        return EvaluateResults(stopResult.ElapsedMs, startResult.ElapsedMs, readResult);
    }

    /// <summary>
    /// Симулирует разрыв связи через остановку диспетчера.
    /// </summary>
    private async Task<OperationResult> SimulateDisconnectAsync()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (dispatcher.IsStarted)
            {
                logger.LogInformation("Останавливаем диспетчер...");
                await dispatcher.StopAsync();
            }

            sw.Stop();

            return new OperationResult
            {
                Success = true,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Ошибка при остановке диспетчера: {Error}", ex.Message);
            return new OperationResult
            {
                Success = false,
                Error = ex.Message,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Запускает диспетчер и ожидает восстановления связи.
    /// </summary>
    private async Task<OperationResult> StartAndWaitForRecoveryAsync(TestStepContext context, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            logger.LogInformation("Запускаем диспетчер...");
            await dispatcher.StartAsync(ct);

            var waited = 0;

            while (!dispatcher.IsConnected && waited < RecoveryTimeoutMs)
            {
                ct.ThrowIfCancellationRequested();
                await context.DelayAsync(TimeSpan.FromMilliseconds(PollIntervalMs), ct);
                waited += PollIntervalMs;
                logger.LogDebug("Ожидание восстановления... {Waited}ms, IsConnected={IsConnected}",
                    waited, dispatcher.IsConnected);
            }

            sw.Stop();

            if (!dispatcher.IsConnected)
            {
                logger.LogError("Таймаут восстановления: {Timeout}ms", RecoveryTimeoutMs);
                return new OperationResult
                {
                    Success = false,
                    Error = $"Не удалось восстановить связь за {RecoveryTimeoutMs}ms",
                    ElapsedMs = sw.ElapsedMilliseconds
                };
            }

            logger.LogInformation("Связь восстановлена за {Elapsed}ms", sw.ElapsedMilliseconds);
            return new OperationResult
            {
                Success = true,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Ошибка при запуске диспетчера: {Error}", ex.Message);
            return new OperationResult
            {
                Success = false,
                Error = ex.Message,
                ElapsedMs = sw.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Верифицирует соединение через тестовое чтение.
    /// </summary>
    private async Task<bool> VerifyConnectionWithReadAsync(TestStepContext context, CancellationToken ct)
    {
        logger.LogInformation("Проверяем связь чтением регистра...");

        var result = await context.DiagReader.ReadUInt16Async(TestAddress, ct: ct);

        if (result.Success)
        {
            logger.LogInformation("Чтение успешно: 0x{Value:X4}", result.Value);
            return true;
        }

        logger.LogWarning("Чтение не удалось: {Error}", result.Error);
        return false;
    }

    /// <summary>
    /// Оценивает результаты теста.
    /// </summary>
    private TestStepResult EvaluateResults(long stopMs, long recoveryMs, bool readSuccess)
    {
        var summary = $"Stop={stopMs}ms, Recovery={recoveryMs}ms, ReadVerify={readSuccess}";

        logger.LogInformation(
            "◼ Результаты теста восстановления:\n" +
            "  Остановка: {StopMs}ms\n" +
            "  Восстановление: {RecoveryMs}ms\n" +
            "  Верификация чтением: {ReadSuccess}",
            stopMs, recoveryMs, readSuccess);

        if (!readSuccess)
        {
            return TestStepResult.Fail($"Чтение после восстановления не удалось. {summary}");
        }

        return TestStepResult.Pass(summary);
    }

    /// <summary>
    /// Результат операции.
    /// </summary>
    private struct OperationResult
    {
        public bool Success;
        public string? Error;
        public long ElapsedMs;
    }
}
