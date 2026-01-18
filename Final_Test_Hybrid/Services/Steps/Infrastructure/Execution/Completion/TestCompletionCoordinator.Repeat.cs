using Final_Test_Hybrid.Models.Plc.Tags;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator
{
    private async Task<CompletionResult> HandleNokRepeatAsync(CancellationToken ct)
    {
        logger.LogInformation("NOK повтор: начало процесса сохранения и подготовки");

        // 1. Сохранить NOK результат
        var saved = await TrySaveWithRetryAsync(2, ct);
        if (!saved)
        {
            logger.LogWarning("NOK повтор: сохранение отменено");
            return CompletionResult.Cancelled;
        }

        // 2. ReworkDialog (только MES)
        if (deps.AppSettings.UseMes)
        {
            var handler = OnReworkDialogRequested;
            if (handler == null)
            {
                logger.LogWarning("NOK повтор: нет подписчика на OnReworkDialogRequested");
                return CompletionResult.Cancelled;
            }

            logger.LogInformation("NOK повтор: запуск ReworkDialog");
            var reworkResult = await handler("NOK результат - требуется доработка");

            if (!reworkResult.IsSuccess)
            {
                logger.LogWarning("NOK повтор: ReworkDialog отменён");
                return CompletionResult.Cancelled;
            }
            logger.LogInformation("NOK повтор: ReworkDialog завершён успешно");
        }

        // 3. AskRepeat = true (PLC сбросит Req_Repeat)
        await deps.PlcService.WriteAsync(BaseTags.AskRepeat, true, ct);
        logger.LogInformation("NOK повтор: AskRepeat = true записан, возврат к подготовке");

        return CompletionResult.NokRepeatRequested;
    }
}
