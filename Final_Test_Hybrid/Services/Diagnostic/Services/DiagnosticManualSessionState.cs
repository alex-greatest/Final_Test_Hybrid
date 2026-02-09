namespace Final_Test_Hybrid.Services.Diagnostic.Services;

/// <summary>
/// Runtime-контекст ручной диагностики.
/// Используется для временного отключения автоматических диагностических реакций.
/// </summary>
public sealed class DiagnosticManualSessionState
{
    private int _connectionTestTabCounter;

    /// <summary>
    /// Признак активной вкладки "Тест связи".
    /// </summary>
    public bool IsConnectionTestActive => Volatile.Read(ref _connectionTestTabCounter) > 0;

    /// <summary>
    /// Отмечает вход во вкладку "Тест связи".
    /// </summary>
    public void EnterConnectionTest()
    {
        Interlocked.Increment(ref _connectionTestTabCounter);
    }

    /// <summary>
    /// Отмечает выход из вкладки "Тест связи".
    /// </summary>
    public void ExitConnectionTest()
    {
        var nextValue = Interlocked.Decrement(ref _connectionTestTabCounter);
        if (nextValue >= 0)
        {
            return;
        }

        Interlocked.Exchange(ref _connectionTestTabCounter, 0);
    }
}
