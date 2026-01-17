namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Completion;

/// <summary>
/// Фаза процесса завершения теста.
/// </summary>
public enum CompletionPhase
{
    None,               // Нет активного завершения
    WaitingForPlcEnd,   // Ожидание сброса End от PLC
    SavingToStorage,    // Сохранение в MES/БД
    Completed           // Завершено
}

/// <summary>
/// Результат процесса завершения теста.
/// </summary>
public enum CompletionResult
{
    Finished,           // Тест завершён, выполнен сброс
    RepeatRequested,    // Запрошен повтор теста
    Cancelled           // Отменено (сброс PLC во время ожидания)
}
