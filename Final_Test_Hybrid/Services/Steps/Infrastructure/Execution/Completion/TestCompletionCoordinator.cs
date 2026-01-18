using Final_Test_Hybrid.Services.Common.Logging;
using Final_Test_Hybrid.Services.SpringBoot.Operation;

namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

public partial class TestCompletionCoordinator(
    TestCompletionDependencies deps,
    TestCompletionUiState uiState,
    DualLogger<TestCompletionCoordinator> logger)
{
    public bool IsWaitingForCompletion { get; private set; }

    /// <summary>
    /// Событие запроса диалога ошибки сохранения.
    /// Возвращает true = повторить, false = отменено.
    /// </summary>
    public event Func<string?, Task<bool>>? OnSaveErrorDialogRequested;

    /// <summary>
    /// Событие запроса диалога ошибки подготовки (при NOK повторе).
    /// Возвращает true = повторить, false = отменено.
    /// </summary>
    public event Func<string?, Task<bool>>? OnPrepareErrorDialogRequested;

    /// <summary>
    /// Событие запроса ReworkDialog (для NOK повтора с MES).
    /// Возвращает результат выполнения Rework flow.
    /// </summary>
    public event Func<string, Task<ReworkFlowResult>>? OnReworkDialogRequested;

    /// <summary>
    /// Показывает диалог ошибки подготовки через событие.
    /// </summary>
    public async Task<bool> ShowPrepareErrorDialogAsync(string? errorMessage)
    {
        var handler = OnPrepareErrorDialogRequested;
        if (handler == null)
        {
            logger.LogWarning("Нет подписчика на OnPrepareErrorDialogRequested");
            return false;
        }
        try
        {
            return await handler(errorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ошибка показа диалога подготовки");
            return false;
        }
    }
}
