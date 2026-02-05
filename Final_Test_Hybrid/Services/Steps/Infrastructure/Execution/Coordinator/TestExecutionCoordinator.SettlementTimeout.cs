using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Coordinator;

public partial class TestExecutionCoordinator
{
    private static readonly TimeSpan SettlementPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SettlementTimeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Обрабатывает таймаут ожидания settlement как жёсткую остановку теста.
    /// </summary>
    private int ThrowSettlementTimeout()
    {
        _logger.LogError("Таймаут ожидания settlement карты");
        _testLogger.LogError(null, "Ошибка: таймаут ожидания settlement карты");
        Stop(ExecutionStopReason.Operator, "Таймаут ожидания settlement карты", markFailed: true);
        throw new OperationCanceledException("Settlement timeout", GetCancellationToken());
    }
}