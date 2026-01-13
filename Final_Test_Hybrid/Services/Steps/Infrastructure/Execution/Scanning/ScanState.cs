namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Scanning;

/// <summary>
/// Состояния системы сканирования штрихкодов.
/// Представляет явный state machine вместо комбинации флагов.
/// </summary>
public enum ScanState
{
    /// <summary>
    /// Сканирование отключено.
    /// Причины: оператор не авторизован ИЛИ автомат не готов (TestAskAuto = false).
    /// </summary>
    Disabled,

    /// <summary>
    /// Готов к сканированию.
    /// Оператор авторизован И автомат готов. Ожидание штрихкода.
    /// </summary>
    Ready,

    /// <summary>
    /// Обработка штрихкода.
    /// Выполняется PreExecution pipeline (валидация, загрузка рецептов, etc).
    /// </summary>
    Processing,

    /// <summary>
    /// Тест выполняется.
    /// PreExecution завершён успешно, TestExecutionCoordinator запущен.
    /// </summary>
    TestRunning,

    /// <summary>
    /// Ошибка.
    /// Произошла ошибка во время обработки или выполнения теста.
    /// Ожидание действия пользователя (Retry/Skip/Cancel).
    /// </summary>
    Error,

    /// <summary>
    /// Выполняется сброс теста.
    /// Ввод заблокирован до завершения сброса.
    /// </summary>
    Resetting
}
