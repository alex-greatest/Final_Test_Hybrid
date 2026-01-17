using Final_Test_Hybrid.Services.Common.Logging;

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
}
