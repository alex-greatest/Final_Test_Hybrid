using Final_Test_Hybrid.Models.Plc.Tags;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator
{
    /// <summary>
    /// Обрабатывает NOK повтор: сохраняет результат и сигнализирует PLC.
    /// ReworkDialog будет показан в ScanBarcodeMesStep, если MES потребует.
    /// </summary>
    private async Task<CompletionResult> HandleNokRepeatAsync(CancellationToken ct)
    {
        logger.LogInformation("NOK повтор: начало процесса сохранения");

        // 1. Сохранить NOK результат
        var saved = await TrySaveWithRetryAsync(2, ct);
        if (!saved)
        {
            logger.LogWarning("NOK повтор: сохранение отменено");
            return CompletionResult.Cancelled;
        }

        // 2. AskRepeat = true (PLC сбросит Req_Repeat)
        // ReworkDialog будет показан в ScanBarcodeMesStep если MES потребует
        await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
        logger.LogInformation("NOK повтор: AskRepeat = true, переход к подготовке");

        return CompletionResult.NokRepeatRequested;
    }
}
