namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Триггеры переходов между фазами системы.
/// </summary>
public enum SystemTrigger
{
    // === Activation ===

    /// <summary>
    /// Оператор авторизован + AutoReady включён.
    /// Idle → WaitingForBarcode.
    /// </summary>
    ScanModeEnabled,

    /// <summary>
    /// Оператор вышел или AutoReady выключен.
    /// WaitingForBarcode → Idle.
    /// Очищает CurrentBarcode.
    /// </summary>
    ScanModeDisabled,

    // === Barcode Flow ===

    /// <summary>
    /// Штрихкод отсканирован/введён.
    /// WaitingForBarcode → Preparing.
    /// Устанавливает CurrentBarcode.
    /// </summary>
    BarcodeReceived,

    /// <summary>
    /// Подготовка (ScanStep + BlockBoilerAdapter) завершена успешно.
    /// Preparing → Testing.
    /// </summary>
    PreparationCompleted,

    /// <summary>
    /// Pipeline failed, возврат к сканированию.
    /// Preparing → WaitingForBarcode.
    /// CurrentBarcode сохраняется.
    /// </summary>
    PreparationFailed,

    // === Test Flow ===

    /// <summary>
    /// TestExecutionCoordinator завершил работу.
    /// Testing → Completed.
    /// </summary>
    TestFinished,

    /// <summary>
    /// Оператор запросил повтор теста.
    /// Completed → WaitingForBarcode.
    /// CurrentBarcode сохраняется.
    /// </summary>
    RepeatRequested,

    /// <summary>
    /// Оператор подтвердил результат теста.
    /// Completed → WaitingForBarcode.
    /// CurrentBarcode сохраняется для следующего цикла.
    /// </summary>
    TestAcknowledged,

    // === Reset Flow ===

    /// <summary>
    /// PLC Reset сигнал (жёсткий сброс).
    /// * (except Idle) → Resetting.
    /// Очищает CurrentBarcode после ResetCompleted.
    /// </summary>
    ResetRequestedHard,

    /// <summary>
    /// PLC Reset сигнал (мягкий сброс).
    /// * (except Idle) → Resetting.
    /// CurrentBarcode сохраняется.
    /// </summary>
    ResetRequestedSoft,

    /// <summary>
    /// Мягкий сброс завершён.
    /// Resetting → WaitingForBarcode.
    /// CurrentBarcode сохраняется.
    /// </summary>
    ResetCompletedSoft,

    /// <summary>
    /// Жёсткий сброс завершён.
    /// Resetting → Idle.
    /// CurrentBarcode очищается.
    /// </summary>
    ResetCompletedHard
}
