namespace Final_Test_Hybrid.Services.Steps.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Фазы жизненного цикла системы.
/// НЕ путать с ExecutionState (состояния выполнения теста).
/// </summary>
/// <remarks>
/// Двухуровневая архитектура:
/// - SystemPhase: управляет фазами СИСТЕМЫ (Idle → Testing)
/// - ExecutionState (EXISTS): управляет состояниями ВНУТРИ теста (Running ⇄ PausedOnError)
/// </remarks>
public enum SystemPhase
{
    /// <summary>
    /// Система неактивна. Оператор не авторизован или AutoReady выключен.
    /// Scanner: OFF, Input: DISABLED.
    /// </summary>
    Idle,

    /// <summary>
    /// Ожидание штрихкода. Scanner session активна.
    /// Scanner: ON, Input: ENABLED.
    /// </summary>
    WaitingForBarcode,

    /// <summary>
    /// Выполнение подготовки (ScanStep, BlockBoilerAdapter).
    /// Scanner: ON (для retry), Input: DISABLED.
    /// </summary>
    Preparing,

    /// <summary>
    /// Выполнение тестовых шагов. TestExecutionCoordinator активен.
    /// Scanner: OFF, Input: DISABLED.
    /// </summary>
    Testing,

    /// <summary>
    /// Тест завершён. Ожидание подтверждения/повтора.
    /// Scanner: OFF, Input: DISABLED.
    /// </summary>
    Completed,

    /// <summary>
    /// Выполняется сброс по сигналу PLC.
    /// Scanner: OFF, Input: DISABLED.
    /// </summary>
    Resetting
}
